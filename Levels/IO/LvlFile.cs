﻿/*
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
using System.IO;
using System.IO.Compression;
using System.Linq;

namespace MCGalaxy.Levels.IO {

    public static class LvlFile {
        
        public static void Save(Level level, string file) {
            using (Stream fs = File.Create(file),
                   gs = new GZipStream(fs, CompressionMode.Compress, true))
            {
                byte[] header = new byte[16];
                BitConverter.GetBytes(1874).CopyTo(header, 0);
                gs.Write(header, 0, 2);

                BitConverter.GetBytes(level.width).CopyTo(header, 0);
                BitConverter.GetBytes(level.height).CopyTo(header, 2);
                BitConverter.GetBytes(level.depth).CopyTo(header, 4);
                BitConverter.GetBytes(level.spawnx).CopyTo(header, 6);
                BitConverter.GetBytes(level.spawnz).CopyTo(header, 8);
                BitConverter.GetBytes(level.spawny).CopyTo(header, 10);
                header[12] = level.rotx;
                header[13] = level.roty;
                header[14] = (byte)level.permissionvisit;
                header[15] = (byte)level.permissionbuild;
                gs.Write(header, 0, header.Length);
                byte[] blocks = level.blocks;
                byte[] convBlocks = new byte[blocks.Length];

                for (int i = 0; i < blocks.Length; ++i) {
                    byte block = blocks[i];
                    if (block < 66)    {
                        convBlocks[i] = block; //CHANGED THIS TO INCOPARATE SOME MORE SPACE THAT I NEEDED FOR THE door_orange_air ETC.
                    } else {
                        convBlocks[i] = Block.SaveConvert(block);
                    }
                }
                gs.Write(convBlocks, 0, convBlocks.Length);
                
                level.CustomBlocks[0] = 1;
                if (level.CustomBlocks != null) {
                    var chunks = level.CustomBlocks.Split(level.width * level.height * level.depth / 4096);
                    //Identifier
                    gs.WriteByte(2);
                    foreach (var test in chunks) {
                        bool empty = true;
                        foreach (byte a in test) {
                            if (a != 0)
                                empty = false;
                        }
                        if (empty) {
                            gs.WriteByte(0);
                        } else {
                            gs.WriteByte(1);
                            gs.Write(test.ToArray(), 0, test.Count());
                        }
                    }
                }
            }
        }
        
        public static Level Load(string name, string file, bool loadTexturesConfig = true) {
            using (Stream fs = File.OpenRead(file),
                   gs = new GZipStream(fs, CompressionMode.Decompress, true))
            {
                byte[] header = new byte[16];
                gs.Read(header, 0, 2);
                ushort[] vars = new ushort[6];
                vars[0] = BitConverter.ToUInt16(header, 0);

                int offset = 0;
                if (vars[0] == 1874) { // version field, width is next ushort
                    gs.Read(header, 0, 16);
                    vars[0] = BitConverter.ToUInt16(header, 0);
                    offset = 2;
                } else {
                    gs.Read(header, 0, 12);
                }                
                vars[1] = BitConverter.ToUInt16(header, offset);
                vars[2] = BitConverter.ToUInt16(header, offset + 2);

                Level level = new Level(name, vars[0], vars[2], vars[1], 
                                        "full_empty", 0, loadTexturesConfig);
                level.spawnx = BitConverter.ToUInt16(header, offset + 4);
                level.spawny = BitConverter.ToUInt16(header, offset + 6);
                level.spawnz = BitConverter.ToUInt16(header, offset + 8);
                level.rotx = header[offset + 10];
                level.roty = header[offset + 11];
                
                gs.Read(level.blocks, 0, level.blocks.Length);
                try {
                    int chunkSize = level.width * level.length * level.depth / 4096;
                    int curOffset = 0;
                    if (gs.ReadByte() == 2)
                    {
                        for (int i = 1; i <= chunkSize; i++)
                        {
                            curOffset += 1;
                            if (gs.ReadByte() == 1)
                            {
                                gs.Read(level.CustomBlocks, curOffset, chunkSize);
                                curOffset += 16;
                            }
                        }
                    }
                } catch {
                    level.CustomBlocks = null;
                }
                return level;
            }
        }
    }
}