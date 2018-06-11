/*
    Copyright 2010 MCSharp team (Modified for use with MCZall/MCLawl/MCGalaxy)
    
    Dual-licensed under the Educational Community License, Version 2.0 and
    the GNU General Public License, Version 3 (the "Licenses"); you may
    not use this file except in compliance with the Licenses. You may
    obtain a copy of the Licenses at
    
    http://www.opensource.org/licenses/ecl2.php
    http://www.gnu.org/licenses/gpl-3.0.html
    
    Unless required by applicable law or agreed to in writing,
    software distributed under the Licenses are distributed on an "AS IS"
    BASIS, WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express
    or implied. See the Licenses for the specific language governing
    permissions and limitations under the Licenses.
*/
using System;
using System.Collections.Generic;
using System.IO;

namespace MCGalaxy.Commands.Info { 
    public sealed class CmdRankInfo : Command {        
        public override string name { get { return "RankInfo"; } }
        public override string shortcut { get { return "ri"; } }
        public override string type { get { return CommandTypes.Moderation; } }
        public override LevelPermission defaultRank { get { return LevelPermission.AdvBuilder; } }
        public override bool UseableWhenFrozen { get { return true; } }
        
        public override void Use(Player p, string message) {
            if (CheckSuper(p, message, "player name")) return;
            if (message.Length == 0) message = p.name;
            
            List<string> rankings = Server.RankInfo.FindMatches(p, message, "rankings");
            if (rankings == null) return;
            
            string target = PlayerMetaList.GetName(rankings[0]);
            Player.Message(p, "  Rankings for {0}:", PlayerInfo.GetColoredName(p, target));
            
            foreach (string line in rankings) {
                string[] args = line.SplitSpaces();
                int offset;
                TimeSpan delta;
                              
                if (args.Length <= 6) {
                    delta = DateTime.UtcNow - long.Parse(args[2]).FromUnixTime();
                    offset = 3;
                } else {
                    // Backwards compatibility with old format
                    int min = int.Parse(args[2]), hour = int.Parse(args[3]);
                    int day = int.Parse(args[4]), month = int.Parse(args[5]), year = int.Parse(args[6]);
                    delta = DateTime.Now - new DateTime(year, month, day, hour, min, 0);
                    offset = 7;
                }
                
                string newRank = Group.GetColoredName(args[offset]);
                string oldRank = Group.GetColoredName(args[offset + 1]);
                
                offset += 2;
                string reason = args.Length <= offset ? "(no reason given)" : args[offset].Replace("%20", " ");
               
                Player.Message(p, "&aFrom {0} &ato {1} &a{2} ago", 
                               oldRank, newRank, delta.Shorten(true, false));
                Player.Message(p, "&aBy %S{0}&a, reason: %S{1}", 
                               PlayerInfo.GetColoredName(p, args[1]), reason);
            }
        }
        
        public override void Help(Player p) {
            Player.Message(p, "%T/RankInfo [player]");
            Player.Message(p, "%HReturns details about that person's rankings.");
        }
    }
}
