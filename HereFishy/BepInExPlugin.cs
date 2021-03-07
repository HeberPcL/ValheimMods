﻿using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using System;
using System.Collections;
using System.IO;
using System.Reflection;
using UnityEngine;
using UnityEngine.Networking;

namespace HereFishy
{
    [BepInPlugin("aedenthorn.HereFishy", "Here Fishy", "0.1.6")]
    public class BepInExPlugin : BaseUnityPlugin
    {
        private static readonly bool isDebug = true;

        public static ConfigEntry<bool> modEnabled;
        public static ConfigEntry<int> nexusID;

        public static ConfigEntry<string> hotKey;
        public static ConfigEntry<bool> playHereFishy;
        public static ConfigEntry<bool> playWeeee;
        public static ConfigEntry<float> hereFishyVolume;
        public static ConfigEntry<float> weeVolume;
        public static ConfigEntry<float> maxFishyDistance;
        public static ConfigEntry<float> jumpSpeed;
        public static ConfigEntry<float> jumpHeight;

        private static BepInExPlugin context;
        private static AudioClip fishyClip;
        private static AudioClip weeClip;
        private static Fish currentFish;
        private static Vector3 origPos;
        private static Vector3 flatPos;
        private static bool hereFishying;
        private static AudioSource playerAudio;
        private static AudioSource fishAudio;

        public static void Dbgl(string str = "", bool pref = true)
        {
            if (isDebug)
                Debug.Log((pref ? typeof(BepInExPlugin).Namespace + " " : "") + str);
        }
        private void Awake()
        {
            context = this;
            
            modEnabled = Config.Bind<bool>("General", "Enabled", true, "Enable this mod");
            nexusID = Config.Bind<int>("General", "NexusID", 218, "Nexus mod ID for updates");
            
            hotKey = Config.Bind<string>("General", "HotKey", "g", "Heeeeeeeeeeeeeeeeeeeeeeeeeeere Fishy Fishy Fishy key. Use https://docs.unity3d.com/Manual/ConventionalGameInput.html");
            maxFishyDistance = Config.Bind<float>("General", "MaxFishyDistance", 100f, "Max distance Heeeeeeeeeeeeeeeeeeeeeeeeeeere Fishy Fishy Fishy can be heard");
            playHereFishy = Config.Bind<bool>("General", "PlayHereFishy", true, "Heeeeeeeeeeeeeeeeeeeeeeeeeeere Fishy Fishy Fishy");
            playWeeee = Config.Bind<bool>("General", "PlayWeeee", true, "Weeeeeeeeeeeeeeeeeeeee");
            hereFishyVolume = Config.Bind<float>("General", "HereFishyVolume", 1f, "Heeeeeeeeeeeeeeeeeeeeeeeeeeere Fishy Fishy Fishy volume");
            weeVolume = Config.Bind<float>("General", "WeeVolume", 1f, "Weeeeeeeeeeeeeeeeeeeee volume");
            jumpSpeed = Config.Bind<float>("General", "JumpSpeed", 0.1f, "Fishy jump speed");
            jumpHeight = Config.Bind<float>("General", "JumpHeight", 6f, "Fishy jump height");

            if (!modEnabled.Value)
                return;
            
            StartCoroutine(PreloadClipsCoroutine());

            Harmony.CreateAndPatchAll(Assembly.GetExecutingAssembly(), null);

        }
        private void Update()
        {
            if (!modEnabled.Value || Player.m_localPlayer == null || !Traverse.Create(Player.m_localPlayer).Method("TakeInput").GetValue<bool>())
                return;

            if (AedenthornUtils.CheckKeyDown(hotKey.Value))
            {
                Dbgl($"pressed hotkey");
                Traverse.Create(Player.m_localPlayer).Field("m_guardianPowerCooldown").SetValue(0);
                float closest = maxFishyDistance.Value;
                Fish closestFish = null;
                foreach (Collider collider in Physics.OverlapSphere(Player.m_localPlayer.transform.position, maxFishyDistance.Value))
                {
                    Fish fish = collider.transform.parent?.gameObject?.GetComponent<Fish>();
                    if (fish?.GetComponent<ZNetView>()?.IsValid() == true)
                    {
                        //Dbgl($"got fishy at {fish.gameObject.transform.position}");

                        float distance = Vector3.Distance(Player.m_localPlayer.transform.position, fish.gameObject.transform.position);
                        if (distance < closest)
                        {
                            //Dbgl($"closest fishy");
                            closest = distance;
                            closestFish = fish;
                        }
                    }
                }
                if (closestFish != null)
                {
                    Dbgl($"got closest fishy at {closestFish.gameObject.transform.position}");

                    currentFish = closestFish;
                    hereFishying = true;
                    if (playHereFishy.Value && fishyClip != null)
                    {
                        ((ZSyncAnimation)typeof(Player).GetField("m_zanim", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(Player.m_localPlayer)).SetTrigger("gpower");
                        Destroy(Player.m_localPlayer.gameObject.GetComponent<AudioSource>());
                        playerAudio = Player.m_localPlayer.gameObject.AddComponent<AudioSource>();
                        playerAudio.volume = Mathf.Clamp(hereFishyVolume.Value, 0.1f, 1f);
                        playerAudio.clip = fishyClip;
                        playerAudio.loop = false;
                        playerAudio.spatialBlend = 1f;
                        playerAudio.Play();

                        Invoke("StartJump", fishyClip.length);
                    }
                    else
                    {
                        Invoke("StartJump", 1);
                    }
                }
            }
        }
        private void StartJump()
        {
            Dbgl("starting fish jump");
            Destroy(playerAudio);
            hereFishying = false;
            if (playWeeee.Value)
            {
                fishAudio = currentFish.gameObject.AddComponent<AudioSource>();
                fishAudio.volume = Mathf.Clamp(weeVolume.Value, 0.1f, 1f);
                fishAudio.clip = weeClip;
                fishAudio.loop = false;
                fishAudio.spatialBlend = 1f;
                fishAudio.Play();
            }
            origPos = currentFish.gameObject.transform.position;
            flatPos = origPos;
            context.StartCoroutine(FishJump());
        }

        private static IEnumerator FishJump()
        {
            for (; ; )
            {
                flatPos = Vector3.MoveTowards(flatPos, Player.m_localPlayer.transform.position, jumpSpeed.Value);

                Vector3 playerPos = Player.m_localPlayer.transform.position;

                float travelled = Vector3.Distance(flatPos, origPos);
                float total = Vector3.Distance(playerPos, origPos);

                float height = (float)Math.Sin(travelled * Math.PI / total) * jumpHeight.Value;

                try
                {
                    currentFish.gameObject.transform.position = new Vector3(flatPos.x, flatPos.y + height, flatPos.z);
                }
                catch
                {
                    break;
                }

                if (Vector3.Distance(playerPos, currentFish.gameObject.transform.position) < jumpSpeed.Value * 20)
                {
                    
                }

                if (Vector3.Distance(playerPos, currentFish.gameObject.transform.position) < jumpSpeed.Value)
                {
                    Dbgl("taking fish");
                    Destroy(fishAudio);
                    currentFish.Pickup(Player.m_localPlayer);
                    break;
                }
                yield return null;
            }
        }



        public static IEnumerator PreloadClipsCoroutine()
        {
            string path = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "HereFishy", "herefishy.wav");

            if (!File.Exists(path))
            {
                Dbgl($"file {path} does not exist!");
                yield break;
            }
            string filename = "file:///" + path.Replace("\\", "/");
            Dbgl($"getting audio clip from filename: {filename}");



            using (UnityWebRequest www = UnityWebRequestMultimedia.GetAudioClip(filename, AudioType.WAV))
            {
                www.SendWebRequest();
                yield return null;

                if (www != null)
                {

                    DownloadHandlerAudioClip dac = ((DownloadHandlerAudioClip)www.downloadHandler);
                    if (dac != null)
                    {
                        AudioClip ac = dac.audioClip;
                        if (ac != null)
                        {
                            Dbgl("audio clip is not null. samples: " + ac.samples);
                            fishyClip = ac;
                        }
                        else
                        {
                            Dbgl("audio clip is null. data: " + dac.text);
                        }
                    }
                    else
                    {
                        Dbgl("DownloadHandler is null. bytes downloaded: " + www.downloadedBytes);
                    }
                }
                else
                {
                    Dbgl("www is null " + www.url);
                }
            }
            
            path = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "HereFishy", "wee.wav");

            filename = "file:///" + path.Replace("\\", "/");

            Dbgl($"filename: {filename}");

            using (UnityWebRequest www = UnityWebRequestMultimedia.GetAudioClip(filename, AudioType.WAV))
            {

                www.SendWebRequest();
                yield return null;
                //Dbgl($"checking downloaded {filename}");
                if (www != null)
                {
                    //Dbgl("www not null. errors: " + www.error);
                    DownloadHandlerAudioClip dac = ((DownloadHandlerAudioClip)www.downloadHandler);
                    if (dac != null)
                    {
                        AudioClip ac = dac.audioClip;
                        if (ac != null)
                        {
                            Dbgl("audio clip is not null. samples: " + ac.samples);
                            weeClip = ac;
                        }
                        else
                        {
                            Dbgl("audio clip is null. data: " + dac.text);
                        }
                    }
                    else
                    {
                        Dbgl("DownloadHandler is null. bytes downloaded: " + www.downloadedBytes);
                    }
                }
                else
                {
                    Dbgl("www is null " + www.url);
                }
            }
        }


        [HarmonyPatch(typeof(CharacterAnimEvent), "GPower")]
        static class CharacterAnimEvent_GPower_Patch
        {
            static bool Prefix()
            {
                return (!hereFishying);
            }
        }

        [HarmonyPatch(typeof(Console), "InputText")]
        static class InputText_Patch
        {
            static bool Prefix(Console __instance)
            {
                if (!modEnabled.Value)
                    return true;
                string text = __instance.m_input.text;
                if (text.ToLower().Equals("herefishy reset"))
                {
                    context.Config.Reload();
                    context.Config.Save();
                    Traverse.Create(__instance).Method("AddString", new object[] { text }).GetValue();
                    Traverse.Create(__instance).Method("AddString", new object[] { "Here Fishy config reloaded" }).GetValue();
                    return false;
                }
                return true;
            }
        }
    }
}