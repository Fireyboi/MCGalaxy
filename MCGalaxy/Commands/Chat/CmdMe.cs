/*
    Copyright 2010 MCSharp team (Modified for use with MCZall/MCLawl/MCGalaxy)
    
    Dual-licensed under the    Educational Community License, Version 2.0 and
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
using MCGalaxy.Events.PlayerEvents;

namespace MCGalaxy.Commands.Chatting {
    public sealed class CmdMe : MessageCmd {
        public override string name { get { return "Me"; } }
        public override string type { get { return CommandTypes.Chat; } }
        public override LevelPermission defaultRank { get { return LevelPermission.Guest; } }
        public override bool SuperUseable { get { return false; } }
        
        public override void Use(Player p, string message) {
            if (message.Length == 0) { Player.Message(p, "You"); return; }
            if (p.joker) { Player.Message(p, "Cannot use /me while jokered."); return; }
            
            string msg = p.color + "*" + Colors.Strip(p.DisplayName) + " " + message;
            if (TryMessage(p, msg))
                OnPlayerActionEvent.Call(p, PlayerAction.Me, message);
        }
        
        public override void Help(Player p) {
            Player.Message(p, "What do you need help with, m'boy?! Are you stuck down a well?!");
        }
    }
}