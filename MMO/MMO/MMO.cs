using MCGalaxy;

namespace MMO
{
    public sealed class MMOPlugin : Plugin
    {
        public override string creator { get { return "Venk"; } }
        public override string MCGalaxy_Version { get { return "1.9.1.2"; } }
        public override string name { get { return "MMO"; } }

        public static string secretCode = "";

        public override void Load(bool startup)
        {
            Quests.Load();
            Command.Register(new CmdQuest());
            Command.Register(new CmdQuests());
            Command.Register(new CmdObjectives());
        }

        public override void Unload(bool shutdown)
        {
            Command.Unregister(Command.Find("Quest"));
            Command.Unregister(Command.Find("Quests"));
            Command.Unregister(Command.Find("Objectives"));
        }
    }
}