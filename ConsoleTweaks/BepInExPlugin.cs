﻿using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;

namespace ConsoleTweaks
{
    [BepInPlugin("aedenthorn.ConsoleTweaks", "Console Tweaks", "0.2.1")]
    public class BepInExPlugin : BaseUnityPlugin
    {
        private static readonly bool isDebug = true;
        private static BepInExPlugin context;

        public static ConfigEntry<bool> modEnabled;
        public static ConfigEntry<bool> cheatsEnabled;
        public static ConfigEntry<bool> debugEnabled;
        public static ConfigEntry<bool> skEnabled;
        public static ConfigEntry<int> nexusID;

        public static string spawnString = "";
        public static string commandString = "";
        public static string spawnSuffix = "";
        public static string commandSuffix = "";

        public static List<string> spawnStrings = new List<string>();
        public static List<string> skCommandStrings = new List<string>()
        {
            "/alt",
            "/coords",
            "/clear",
            "/clearinventory",
            "/detect",
            "/env",
            "/event",
            "/farinteract",
            "/findtomb",
            "/fly",
            "/freecam",
            "/ghost",
            "/give",
            "/god",
            "/heal",
            "/imacheater",
            "/infstam",
            "/killall",
            "/listitems",
            "/listskills",
            "/nocost",
            "/nores",
            "/nosup",
            "/portals",
            "/q",
            "/randomevent",
            "/removedrops",
            "/repair",
            "/resetmap",
            "/resetwind",
            "/revealmap",
            "/seed",
            "/set",
            "/set",
            "/set",
            "/set",
            "/set",
            "/set",
            "/set",
            "/spawn",
            "/stopevent",
            "/td",
            "/tl",
            "/tr",
            "/tu",
            "/tame",
            "/tod",
            "/tp",
            "/wind",
            "/whois",
        };
        public static List<string> basicCommandStrings = new List<string>()
        {
            "help",
            "kick",
            "ban",
            "unban",
            "banned",
            "ping",
            "lodbias",
            "info"
        };
        public static List<string> cheatCommandStrings = new List<string>()
        {

            "genloc",
            "debugmode",
            "spawn",
            "pos",
            "goto",
            "exploremap",
            "resetmap",
            "killall",
            "tame",
            "hair",
            "beard",
            "location",
            "raiseskill",
            "resetskill",
            "freefly",
            "ffsmooth",
            "tod",
            "env",
            "resetenv",
            "wind",
            "resetwind",
            "god",
            "event",
            "stopevent",
            "randomevent",
            "save",
            "resetcharacter",
            "removedrops",
            "setkey",
            "resetkeys",
            "listkeys",
            "players",
            "dpsdebug"
        };
        public static List<string> commandStrings = new List<string>();

        public static void Dbgl(string str = "", bool pref = true)
        {
            if (isDebug)
                Debug.Log((pref ? typeof(BepInExPlugin).Namespace + " " : "") + str);
        }
        private void Awake()
        {
            context = this;
            modEnabled = Config.Bind<bool>("General", "Enabled", true, "Enable this mod");
            cheatsEnabled = Config.Bind<bool>("General", "CheatsEnabled", true, "Enable cheats by default");
            //debugEnabled = Config.Bind<bool>("General", "DebugEnabled", false, "Enable debug mode by default");
            skEnabled = Config.Bind<bool>("General", "SkEnabled", true, "Enable SkToolbox command completion");
            nexusID = Config.Bind<int>("General", "NexusID", 464, "Nexus mod ID for updates");

            if (!modEnabled.Value)
                return;

            //if (debugEnabled.Value)
            //    Player.m_debugMode = true;

            Harmony.CreateAndPatchAll(Assembly.GetExecutingAssembly(), null);
        }
        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.F5) && Console.instance.m_chatWindow.gameObject.activeSelf) 
            {
                Dbgl($"Opening console");

                commandStrings = new List<string>(basicCommandStrings);
                if (skEnabled.Value)
                    commandStrings.AddRange(skCommandStrings);

                if (cheatsEnabled.Value)
                    Traverse.Create(Console.instance).Field("m_cheat").SetValue(true);

                if(Traverse.Create(Console.instance).Field("m_cheat").GetValue<bool>())
                    commandStrings.AddRange(cheatCommandStrings);

                LoadSpawnStrings();
            }
        }

        private void LoadSpawnStrings()
        {
            if (ZNetScene.instance == null)
                return;
            spawnStrings.Clear();
            foreach(GameObject go in Traverse.Create(ZNetScene.instance).Field("m_namedPrefabs").GetValue<Dictionary<int, GameObject>>().Values)
            {
                spawnStrings.Add(ZNetView.GetPrefabName(go));
            }
            Dbgl($"Loaded {spawnStrings.Count} strings");
        }

        [HarmonyPatch(typeof(Console), "Update")]
        static class UpdateChat_Patch
        {
            static void Postfix(Console __instance)
            {
                if (!modEnabled.Value || !Console.instance.m_chatWindow.gameObject.activeSelf)
                    return;
                string str = __instance.m_input.text;
                string[] words = str.Split(' ');

                if (Input.GetKeyDown(KeyCode.Tab))
                {
                    int caret = __instance.m_input.caretPosition;
                    string afterCaret = str.Substring(caret);
                    int space = afterCaret.IndexOf(' ');
                    if (space == 0)
                        space = 1;
                    else if (space == -1)
                        space = afterCaret.Length;
                    __instance.m_input.caretPosition += space;
                }

                string strToCaret = str.Substring(0, __instance.m_input.caretPosition);
                string[] wordsToCaret = strToCaret.Split(' ');
                string suffix = str.Substring(__instance.m_input.caretPosition).Split(' ')[0];
                if (wordsToCaret.Length == 1)
                {
                    string prefix = wordsToCaret[0];
                    if (suffix == commandSuffix)
                        words[0] = prefix;
                    if (commandString != prefix)
                    {
                        if(prefix.Length > 0)
                        {
                            string exact = commandStrings.Find(s => s.ToLower() == words[0].ToLower());
                            string partial = commandStrings.Find(s => s.ToLower().StartsWith(wordsToCaret[0].ToLower()));
                            if (partial != null && exact == null)
                            {
                                commandSuffix = partial.Substring(prefix.Length);
                                if (commandSuffix.Length > 0)
                                {
                                    words[0] = partial;
                                }
                            }
                        }
                        __instance.m_input.text = string.Join(" ", words);
                        commandString = prefix;
                    }
                }
                else if (wordsToCaret.Length == 2 && wordsToCaret[0] == "spawn")
                {
                    string prefix = wordsToCaret[1];
                    if (suffix == spawnSuffix)
                        words[1] = prefix;
                    if (spawnString != prefix)
                    {
                        if (prefix.Length > 0)
                        {
                            string exact = spawnStrings.Find(s => s.ToLower() == words[1].ToLower());
                            string partial = spawnStrings.Find(s => s.ToLower().StartsWith(wordsToCaret[1].ToLower()));
                            if (partial != null && exact == null)
                            {
                                spawnSuffix = partial.Substring(prefix.Length);
                                if (spawnSuffix.Length > 0)
                                {
                                    //Dbgl($"got match {partial}");
                                    words[1] = partial;
                                }
                            }
                        }
                        __instance.m_input.text = string.Join(" ", words);
                        spawnString = prefix;
                    }
                }
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
                if (text.ToLower().Equals("consoletweaks reset"))
                {
                    context.Config.Reload();
                    context.Config.Save();
                    //if (debugEnabled.Value)
                    //    Player.m_debugMode = true;
                    Traverse.Create(__instance).Method("AddString", new object[] { text }).GetValue();
                    Traverse.Create(__instance).Method("AddString", new object[] { "config reloaded" }).GetValue();
                    return false;
                }
                return true;
            }
        }
    }
}