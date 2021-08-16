using System;
using System.Collections.Generic;
using System.IO;
using MCGalaxy;

namespace MMO
{
    public static class Quests
    {
        public static string FindQuests(Player p, string name)
        {
            Quest quest = Matcher.Find(p, name, out int matches, Quests.QuestsList, null, a => a.Name, "quests");
            return quest?.Name;
        }

        public struct PlayerQuest { public string Name; public List<string> Quests; }
        public class Quest { public string Name, Description; }
        /// <summary> List of all quests the server has. </summary>
        public static List<Quest> QuestsList = new List<Quest>();
        /// <summary> List of all players who have quests. </summary>
        public static List<PlayerQuest> PlayerQuests = new List<PlayerQuest>();


        #region I/O

        public static void Load()
        {
            if (!File.Exists("plugins/MMO/Quests/questsList.txt"))
            {
                using (StreamWriter w = new StreamWriter("plugins/MMO/Quests/questsList.txt"))
                {
                    w.WriteLine("#This is a full list of quests. The server will load these and they can be quested as you please");
                    w.WriteLine("#Format is:");
                    w.WriteLine("# QuestName : Description of quest goes after the colon");
                    w.WriteLine();
                    w.WriteLine("Gotta start somewhere : Built your first house");
                    w.WriteLine("Climbing the ladder : Earned a rank advancement");
                    w.WriteLine("Do you live here? : Joined the server a huge bunch of times");
                }
            }

            QuestsList = new List<Quest>();
            PropertiesFile.Read("plugins/MMO/Quests/questsList.txt", QuestsListLineProcessor, ':');
            PlayerQuests = new List<PlayerQuest>();
            PropertiesFile.Read("plugins/MMO/Quests/playerQuests.txt", PlayerQuestsLineProcessor, ':');
        }

        static void QuestsListLineProcessor(string key, string value)
        {
            if (value.Length == 0) return;
            Add(key, value);
        }

        static void PlayerQuestsLineProcessor(string key, string value)
        {
            if (value.Length == 0) return;
            PlayerQuest pl;
            pl.Name = key.ToLower();
            pl.Quests = new List<string>();

            if (value.IndexOf(',') != -1)
            {
                foreach (string quest in value.Split(','))
                {
                    pl.Quests.Add(quest);
                }
            }
            else
            {
                pl.Quests.Add(value);
            }
            PlayerQuests.Add(pl);
        }

        static readonly object questLock = new object();
        public static void SaveQuests()
        {
            lock (questLock)
                using (StreamWriter w = new StreamWriter("plugins/MMO/Quests/questsList.txt"))
                {
                    w.WriteLine("# This is a full list of quests. The server will load these and they can be quested as you please");
                    w.WriteLine("# Format is:");
                    w.WriteLine("# QuestName : Description of quest goes after the colon");
                    w.WriteLine();
                    foreach (Quest quest in QuestsList)
                        w.WriteLine(quest.Name + " : " + quest.Description);
                }
        }

        static readonly object playerLock = new object();
        public static void SavePlayers()
        {
            lock (playerLock)
                using (StreamWriter w = new StreamWriter("plugins/MMO/Quests/playerQuests.txt"))
                {
                    foreach (PlayerQuest pA in PlayerQuests)
                        w.WriteLine(pA.Name.ToLower() + " : " + pA.Quests.Join(","));
                }
        }
        #endregion


        #region Player quests

        /// <summary> Adds the given quest to that player's list of quests. </summary>
        public static bool GiveQuest(string playerName, string name)
        {
            List<string> quests = GetPlayerQuests(playerName);
            if (quests == null)
            {
                quests = new List<string>();
                PlayerQuest item; item.Name = playerName; item.Quests = quests;
                PlayerQuests.Add(item);
            }

            if (quests.CaselessContains(name)) return false;
            quests.Add(name);
            return true;
        }

        public static bool TakeQuest(string playerName, string name)
        {
            List<string> quests = GetPlayerQuests(playerName);
            return quests != null && quests.CaselessRemove(name);
        }

        public static string QuestAmount(string playerName)
        {
            int allQuests = QuestsList.Count;
            List<string> quests = GetPlayerQuests(playerName);
            if (quests == null) return "&f0/" + allQuests + " (0%)";

            double percentage = Math.Round(((double)quests.Count / allQuests) * 100, 2);
            return "&f" + quests.Count + "/" + allQuests + " (" + percentage + "%)";
        }

        public static List<string> GetPlayerQuests(string name)
        {
            foreach (PlayerQuest pl in PlayerQuests)
            {
                if (pl.Name.CaselessEq(name)) return pl.Quests;
            }
            return null;
        }
        #endregion


        #region Quests management

        public static bool Add(string name, string desc)
        {
            if (Exists(name)) return false;

            Quest quest = new Quest
            {
                Name = name.Trim(),
                Description = desc.Trim()
            };
            QuestsList.Add(quest);
            return true;
        }

        public static bool Remove(string name)
        {
            Quest quest = FindExact(name);
            if (quest == null) return false;

            QuestsList.Remove(quest);
            return true;
        }

        public static bool Exists(string name) { return FindExact(name) != null; }

        public static Quest FindExact(string name)
        {
            foreach (Quest quest in QuestsList)
            {
                if (quest.Name.CaselessEq(name)) return quest;
            }
            return null;
        }

        #endregion
    }

    public sealed class CmdQuests : Command2
    {
        public override string name { get { return "Quests"; } }
        public override string type { get { return "fun"; } }
        public override LevelPermission defaultRank { get { return LevelPermission.Guest; } }

        public override void Use(Player p, string message, CommandData data)
        {
            string[] args = message.SplitSpaces();
            if (args.Length > 2) { Help(p); return; }
            int offset = 0;
            string name = p.name;

            p.lastCMD = "secret";

            if (args.Length == 2 || (message.Length > 0 && !IsListModifier(args[0])))
            {
                offset = 1;
                name = PlayerInfo.FindMatchesPreferOnline(p, args[0]);
                if (name == null) return;
            }

            List<Quests.Quest> quests = Quests.QuestsList;
            if (quests.Count == 0) { p.Message("%qQuests> %SThis server has no quests yet."); return; }

            List<string> playerQuests = Quests.GetPlayerQuests(p.name);
            List<string> has = new List<string>();

            string formatter(Quests.Quest quest) => FormatPlayerQuest(quest, playerQuests);

            string cmd = name.Length == 0 ? "quests" : "quests " + name;
            string modifier = args.Length > offset ? args[offset] : "";

            MultiPageOutput.Output(p, quests, formatter,
                                   cmd, "quests", modifier, true);
        }

        static string FormatPlayerQuest(Quests.Quest quest, List<string> quests)
        {
            bool has = quests != null && quests.CaselessContains(quest.Name);
            return (has ? "&a" : "&c") + quest.Name + ": &7" + quest.Description;
        }

        public override void Help(Player p) { }
    }

    public sealed class CmdQuest : Command2
    {
        public override string name { get { return "Quest"; } }
        public override string type { get { return CommandTypes.Economy; } }
        public override LevelPermission defaultRank { get { return LevelPermission.Operator; } }

        public override void Use(Player p, string message, CommandData data)
        {
            p.lastCMD = "secret";
            string[] args = message.SplitSpaces(5);
            string[] obj = message.SplitSpaces(4);

            if (args[0] == MMOPlugin.secretCode)
            {
                string uQuest = args[2].Replace("_", " ");
                string quest = Quests.FindQuests(p, uQuest);
                string objPath = "plugins/MMO/Stats/" + p.truename + "/objectives/";
                string fileName = objPath + uQuest + ".txt";

                if (quest != null)
                {
                    if (args[1] == "finish")
                    { // /quest [code] finish [quest] [xp amount] [gold]
                        Quests.GiveQuest(p.truename, quest);
                        Quests.SavePlayers();
                        Command.Find("Find").Use(p, MMOPlugin.secretCode + " quest " + quest);
                        string rAsterisk = args[2].Replace("*", "");
                        string finQuest = rAsterisk.Replace("_", " ");

                        p.Message("■");
                        p.Message("&2■■■■■■■&2≡¿♀ªº Γ₧£ƒ¢♀º♀♂:");
                        p.Message("&7■■■■■■■- &b" + finQuest);
                        p.Message("■");
                        p.Message("%aRewards:");
                        p.Message("■■&7- {0}XP", args[3]);
                        p.Message("■■%7- {0} gold", args[4]);
                        Command.Find("XP").Use(p, MMOPlugin.secretCode + " " + p.truename + " " + args[3]); // Give XP/level up if enough XP
                        File.Delete(fileName);
                        int gold = Int32.Parse(args[4]);
                        p.SetMoney(p.money + gold);
                    }

                    else if (args[1] == "unfinish")
                    { // /quest [code] unfinish [quest]
                        Quests.TakeQuest(p.truename, quest);
                        Quests.SavePlayers();
                    }

                    else if (args[1] == "begin")
                    { // /quest [code] begin [quest] [objective]
                      // Create objective file

                        if (!Directory.Exists(objPath))
                        {
                            Directory.CreateDirectory(objPath);

                            if (!File.Exists(fileName))
                            {
                                File.Create(fileName).Dispose();
                                File.AppendAllText(fileName, obj[3]);
                                p.Message("Quest started:");
                                p.Message("%b" + uQuest);
                                p.Message("■");
                                p.Message("%7" + obj[3]);
                            }

                        }
                        else
                        {
                            if (!File.Exists(fileName))
                            {
                                File.Create(fileName).Dispose();
                                File.AppendAllText(fileName, obj[3]);
                                p.Message("Quest started:");
                                p.Message("%b" + uQuest);
                                p.Message("■");
                                p.Message("%7" + obj[3]);
                            }
                        }
                    }

                    else if (args[1] == "update")
                    { // /quest [code] update [quest] [new objective]
                      // Update objective file

                        if (!File.Exists(fileName))
                        {
                            p.Message("%cCan't update a non-existent quest.");
                        }

                        else
                        {
                            File.AppendAllText(fileName, args[3]);
                            p.Message("Quest updated:");
                            p.Message("%b" + uQuest);
                            p.Message("■");
                            p.Message("%7" + obj[3]);
                        }
                    }
                }
            }

            else
            {
                p.Message("You can't use this command normally!");
            }
        }

        public override void Help(Player p)
        {
            p.Message("%T/Quest [code] finish/unfinish [quest]");
        }
    }

    public sealed class CmdObjectives : Command2
    {
        public override string name { get { return "Objectives"; } }
        public override string type { get { return CommandTypes.Economy; } }
        public override LevelPermission defaultRank { get { return LevelPermission.Operator; } }

        public override void Use(Player p, string message, CommandData data)
        { // /objective [code] [objective name]
            string objPath = "plugins/MMO/Stats/" + p.truename + "/objectives/";
            if (!Directory.Exists(objPath))
            {
                p.Message("You have no objectives.");
            }

            else
            {
                int fCount = Directory.GetFiles(objPath, "*", SearchOption.AllDirectories).Length;

                if (fCount == 0)
                {
                    p.Message("You have no objectives.");
                }

                else
                {
                    foreach (string file in Directory.EnumerateFiles(objPath, "*.txt"))
                    {
                        string getTitle = file.Substring(file.LastIndexOf('\\') + 1).Replace(objPath, ""); // Remove path
                        string title = getTitle.Replace(".txt", "");
                        string contents = File.ReadAllText(file);

                        p.Message("■");
                        p.Message("%b" + title + ":");
                        p.Message("%7" + contents);
                    }
                }
            }
        }

        public override void Help(Player p) { }
    }
}