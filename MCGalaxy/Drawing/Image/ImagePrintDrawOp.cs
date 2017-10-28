﻿/*
    Copyright 2015 MCGalaxy
        
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
using Draw = System.Drawing;
using MCGalaxy.Drawing.Brushes;
using MCGalaxy.Maths;

namespace MCGalaxy.Drawing.Ops {

    public class ImagePrintDrawOp : DrawOp {
        public override string Name { get { return "ImagePrint"; } }
        
        public override long BlocksAffected(Level lvl, Vec3S32[] marks) {
            return Source.Width * Source.Height;
        }
        
        internal Draw.Bitmap Source;
        internal bool DualLayer, LayerMode;
        internal string Filename;
        public ImagePalette Palette;
        
        internal Vec3S32 dx, dy, adj;
        IPaletteMatcher selector;
        
        public override void Perform(Vec3S32[] marks, Brush brush, DrawOpOutput output) {
            selector = new RgbPaletteMatcher();
            CalcLayerColors();

            using (PixelGetter getter = new PixelGetter(Source)) {
                getter.Init();
                OutputPixels(getter, output);
            }
            selector = null;
            
            // Put all the blocks in shadow
            ExtBlock rock = (ExtBlock)Block.Stone;
            if (DualLayer) {
                ushort y = (ushort)(Origin.Y + Source.Height);
                for (int i = 0; i < Source.Width; i++) {
                    ushort x = (ushort)(Origin.X + dx.X * i);
                    ushort z = (ushort)(Origin.Z + dx.Z * i);
                    output(Place(x, y, z, rock));
                    
                    x = (ushort)(x + adj.X); z = (ushort)(z + adj.Z);
                    output(Place(x, y, z, rock));
                }
            }
            
            Source.Dispose();
            Source = null;
            if (Filename == "tempImage_" + Player.name)
                File.Delete("extra/images/tempImage_" + Player.name + ".bmp");
            Player.Message(Player, "Finished printing image using {0} palette.", Palette.Name);
        }
        
        void CalcLayerColors() {
            PaletteEntry[] front = new PaletteEntry[Palette.Entries.Length];
            PaletteEntry[] back  = new PaletteEntry[Palette.Entries.Length];
            
            ColorDesc sun  = Colors.ParseHex("FFFFFF");
            ColorDesc dark = Colors.ParseHex("9B9B9B");
            if (Utils.IsValidHex(Level.Config.LightColor)) {
                sun = Colors.ParseHex(Level.Config.LightColor);
            }
            if (Utils.IsValidHex(Level.Config.ShadowColor)) {
                dark = Colors.ParseHex(Level.Config.ShadowColor);
            }
            
            for (int i = 0; i < Palette.Entries.Length; i++) {
                PaletteEntry entry = Palette.Entries[i];
                ExtBlock block = ExtBlock.FromRaw(entry.Raw);
                BlockDefinition def = Level.GetBlockDef(block);
                
                if (def != null && def.FullBright) {
                    front[i] = Multiply(entry, Colors.ParseHex("FFFFFF"));
                    back[i]  = Multiply(entry, Colors.ParseHex("FFFFFF"));
                } else {
                    front[i] = Multiply(entry,  sun);
                    back[i]  = Multiply(entry, dark);
                }
            }
            selector.SetPalette(front, back);
        }
        
        static PaletteEntry Multiply(PaletteEntry entry, ColorDesc rgb) {
            entry.R = (byte)(entry.R * rgb.R / 255);
            entry.G = (byte)(entry.G * rgb.G / 255);
            entry.B = (byte)(entry.B * rgb.B / 255);
            return entry;
        }
        
        void OutputPixels(PixelGetter pixels, DrawOpOutput output) {
            int width = pixels.Width, height = pixels.Height;
            for (int yy = 0; yy < height; yy++)
                for (int xx = 0; xx < width; xx++) 
            {
                Pixel P = pixels.Get(xx, yy);
                ushort x = (ushort)(Origin.X + dx.X * xx + dy.X * yy);
                ushort y = (ushort)(Origin.Y + dx.Y * xx + dy.Y * yy);
                ushort z = (ushort)(Origin.Z + dx.Z * xx + dy.Z * yy);
                if (P.A < 20) { output(Place(x, y, z, ExtBlock.Air)); continue; }
                
                byte raw = 0;
                if (!DualLayer) {
                    raw = selector.BestMatch(P.R, P.G, P.B);
                } else {
                    bool backLayer;
                    raw = selector.BestMatch(P.R, P.G, P.B, out backLayer);                    
                    if (backLayer) {
                        x = (ushort)(x + adj.X);
                        z = (ushort)(z + adj.Z);
                    }
                }
                output(Place(x, y, z, ExtBlock.FromRaw(raw)));
            }
        }
        
        public void CalcState(int dir) {
            dx = default(Vec3S32); dy = default(Vec3S32); adj = default(Vec3S32);
            DualLayer = DualLayer && !LayerMode;
            
            // Calculate back layer offset
            if (dir == 0) adj.Z = -1;
            if (dir == 1) adj.Z = +1;
            if (dir == 2) adj.X = +1;
            if (dir == 3) adj.X = -1;
            
            if (LayerMode) {
                if (dir == 0) { dx.X = +1; dy.Z = -1; }
                if (dir == 1) { dx.X = -1; dy.Z = +1; }
                if (dir == 2) { dx.Z = +1; dy.X = +1; }
                if (dir == 3) { dx.Z = -1; dy.X = -1; }
            } else {
                dy.Y = 1; // Oriented upwards
                if (dir == 0) dx.X = +1;
                if (dir == 1) dx.X = -1;
                if (dir == 2) dx.Z = +1;
                if (dir == 3) dx.Z = -1;
            }
        }
    }
}
