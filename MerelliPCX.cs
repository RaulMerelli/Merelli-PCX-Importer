using System;
using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using System.IO;

namespace MerelliPCX
{
    public enum Format
    {
        /// <summary>
        /// ZSoft .pcx Image format.
        /// </summary>
        DEFAULT = 10,
    }
    public enum Version
    {
        /// <summary>
        /// Version 2.5 of PC Paintbrush
        /// </summary>
        VERSION0 = 0,
        /// <summary>
        /// Version 2.8 with palette information
        /// </summary>
        VERSION2 = 2,
        /// <summary>
        /// Version 2.8 without palette information
        /// </summary>
        VERSION3 = 3,
        /// <summary>
        /// PC Paintbrush for Windows
        /// </summary>
        VERSION4 = 4,
        /// <summary>
        /// Version 3.0 and above of PC Paintbrush and PC Paintbrush+, includes Publisher's Paintbrush. Includes 24 - bit.PCX files
        /// </summary>
        VERSION5 = 5,
    }

    public enum Encoding
    {
        /// <summary>
        /// .PCX uncompressed
        /// </summary>
        UNCOMPRESSED = 0,
        /// <summary>
        /// .PCX run length encoding (RLE)
        /// </summary>
        RLE = 1,
    }

    public enum PaletteInfo
    {
        /// <summary>
        /// Ignored
        /// </summary>
        IGNORED = 0,
        /// <summary>
        /// Color or Black and White
        /// </summary>
        COLOR_BW = 1,
        /// <summary>
        /// Grayscale
        /// </summary>
        GRAYSCALE = 2,
    }

    public class PCXImage /*: IDisposable*/
    {
        private Color[] image;
        private List<Color> Palette16;
        private List<Color> Palette256;
        private List<byte> Imagevalues;
        private List<byte> ImagevaluesR;
        private List<byte> ImagevaluesG;
        private List<byte> ImagevaluesB;
        private int Manufacturer;
        private int Version;
        private int Encoding;
        private int Bitsperpixel;
        private int Xmin;
        private int Ymin;
        private int Xmax;
        private int Ymax;
        private int Hdpi;
        private int Vdpi;
        private int Nplanes;
        private int Bytesperline;
        private int Paletteinfo;
        private int Hscreensize;
        private int Vscreensize;
        private int Xsize;
        private int Ysize;
        private int Totalbytes;
        private int Linepaddingsize;
        private byte[] FileArray;
        private bool palette16Empty;
        private bool palette256Empty;
        private Bitmap bmpImage;

        /// <summary>
        /// Gets a Bitmap representation of the loaded file.
        /// </summary>
        public Bitmap Image
        {
            get { return this.bmpImage; }
        }

        private byte value(bool flag, byte datavalue)
        {
            if (flag)
                return datavalue;
            else
                return 0;
        }

        private int Abs(int value)
        {
            if (value < 0)
                return -value;
            else
                return value;
        }

        private int GetPosition(int x, int y, int planelinesize)
        {
            int temp = x;
            while (temp > 8)
                temp -= 8;
            return (y * planelinesize) + x - temp + Abs(temp - 8);
        }

        private void Reset()
        {
            image = null;
            Palette16 = null;
            Palette256 = null;
            Imagevalues = null;
            ImagevaluesR = null;
            ImagevaluesG = null;
            ImagevaluesB = null;
            FileArray = null;
            GC.Collect();
        }

        private void New()
        {
            Palette16 = new List<Color>();
            Palette256 = new List<Color>();
            Imagevalues = new List<byte>();
            ImagevaluesR = new List<byte>();
            ImagevaluesG = new List<byte>();
            ImagevaluesB = new List<byte>();
        }

        private void Header()
        {
            Version = FileArray[1];
            Encoding = FileArray[2];
            Bitsperpixel = FileArray[3];
            Xmin = FileArray[5] << 8 | +FileArray[4];
            Ymin = FileArray[7] << 8 | +FileArray[6];
            Xmax = FileArray[9] << 8 | +FileArray[8];
            Ymax = FileArray[11] << 8 | +FileArray[10];
            Hdpi = FileArray[13] << 8 | +FileArray[12];
            Vdpi = FileArray[15] << 8 | +FileArray[14];
            Nplanes = FileArray[65];
            Bytesperline = FileArray[67] << 8 | FileArray[66];
            Paletteinfo = FileArray[69] << 8 | FileArray[68];
            Hscreensize = FileArray[71] << 8 | FileArray[70];
            Vscreensize = FileArray[73] << 8 | FileArray[72];
            Xsize = Xmax - Xmin + 1;
            Ysize = Ymax - Ymin + 1;
            Totalbytes = Nplanes * Bytesperline;
            Linepaddingsize = (Totalbytes * (8 / Bitsperpixel)) - Xsize;
        }

        private void Palette()
        {
            palette16Empty = true;
            palette256Empty = true;
            if (Version != 3 && Version != 4)
            {
                for (int i = 16; i < 64; i += 3)
                {
                    if (FileArray[i] != 0 && FileArray[i + 1] != 0 && FileArray[i + 2] != 0)
                    {
                        palette16Empty = false;
                    }
                    Palette16.Add(Color.FromArgb(FileArray[i], FileArray[i + 1], FileArray[i + 2]));
                }
            }
            else
            {
                Palette16.Add(Color.FromArgb(0, 0, 0));
                Palette16.Add(Color.FromArgb(0, 0, 255));
                Palette16.Add(Color.FromArgb(0, 255, 0));
                Palette16.Add(Color.FromArgb(0, 255, 255));
                Palette16.Add(Color.FromArgb(255, 0, 0));
                Palette16.Add(Color.FromArgb(255, 0, 255));
                Palette16.Add(Color.FromArgb(255, 255, 0));
                Palette16.Add(Color.FromArgb(255, 255, 255));
                for (int i = 0; i < 8; i++)
                {
                    Palette16.Add(Color.FromArgb(0, 0, 0));
                }
            }

            if (FileArray.Length > 768)
            {
                if (FileArray[FileArray.Length - 768 - 1] == 0x0C)
                {
                    palette256Empty = false;
                    for (int i = FileArray.Length - 768; i < FileArray.Length; i += 3)
                    {
                        Palette256.Add(Color.FromArgb(FileArray[i], FileArray[i + 1], FileArray[i + 2]));
                    }
                }
            }
        }

        private void Decode()
        {
            image = new Color[Xsize * Ysize];
            bmpImage = new Bitmap(Xsize, Ysize);
            int pos = 128;
            byte runcount = 0;
            byte runvalue = 0;

            try
            {
                do
                {
                    byte Byte = FileArray[pos++];
                    if ((Byte & 0xC0) == 0xC0 && pos < FileArray.Length) //2-byte code
                    {
                        runcount = (byte)(Byte & 0x3F); //Get run count
                        runvalue = FileArray[pos++]; //Get pixel value
                    }
                    else //1-byte code
                    {
                        runcount = 1; //Run count is one
                        runvalue = Byte; //Pixel value
                    }
                    for (int j = 0; j < runcount; j++)
                    {
                        Imagevalues.Add(runvalue);
                    }
                } while (pos < FileArray.Length);
            }
            catch
            {
                throw new Exception(@"Error while decoding.");
            }
        }

        private void Fix()
        {
            try
            {
                if (Nplanes == 1 && Bitsperpixel < 3)
                {
                    BitArray bits = new BitArray(Imagevalues.ToArray());
                    Imagevalues.Clear();
                    int planelinesize = (bits.Count / Ysize) / Nplanes;
                    int Position;
                    for (int y = 0; y < Ysize; y++)
                    {
                        for (int x = 1; x < (Xsize * Bitsperpixel) + 1; x += Bitsperpixel)
                        {
                            Position = GetPosition(x, y, planelinesize);
                            switch (Bitsperpixel)
                            {
                                case 1:
                                    Imagevalues.Add(value(bits[Position], 0x1));
                                    break;
                                case 2:
                                    Imagevalues.Add((byte)(value(bits[Position], 0x2) + value(bits[Position - 1], 0x1)));
                                    break;
                            }
                        }
                    }
                }
                else if (Nplanes > 1 && Nplanes < 5 && Bitsperpixel == 1)
                {
                    BitArray bits = new BitArray(Imagevalues.ToArray());
                    Imagevalues.Clear();
                    int planesize = bits.Count / Nplanes;
                    int planelinesize = (bits.Count / Ysize) / Nplanes;
                    int linesize = (bits.Count / Ysize);
                    BitArray[] planes = new BitArray[Nplanes];

                    for (int planeN = 0; planeN < Nplanes; planeN++)
                    {
                        planes[planeN] = new BitArray(planesize);
                        int cnt = 0;
                        for (int y = 0; y < Ysize; y++)
                        {
                            for (int x = 0; x < planelinesize; x++)
                            {
                                planes[planeN][cnt++] = bits[(planelinesize * planeN) + (y * linesize) + x];
                            }
                        }
                    }

                    int Position;
                    for (int y = 0; y < Ysize; y++)
                    {
                        for (int x = 1; x < Xsize + 1; x++)
                        {
                            Position = GetPosition(x, y, planelinesize);
                            if (Nplanes == 4)
                                Imagevalues.Add((byte)(value(planes[3][Position], 0x8) + value(planes[2][Position], 0x4) + value(planes[1][Position], 0x2) + value(planes[0][Position], 0x1)));
                            else if (Nplanes == 3)
                                Imagevalues.Add((byte)(value(planes[2][Position], 0x4) + value(planes[1][Position], 0x2) + value(planes[0][Position], 0x1)));
                            else if (Nplanes == 2)
                                Imagevalues.Add((byte)(value(planes[1][Position], 0x2) + value(planes[0][Position], 0x1)));
                        }
                    }
                }
                else if (Nplanes == 3)
                {
                    ImagevaluesR.AddRange(Imagevalues);
                    ImagevaluesG.AddRange(Imagevalues);
                    ImagevaluesB.AddRange(Imagevalues);
                    Imagevalues.Clear();
                    //RED
                    for (int i = Xsize; i < Xsize * Ysize; i += Xsize)
                    {
                        ImagevaluesR.RemoveRange(i, Linepaddingsize);
                    }
                    //GREEN
                    ImagevaluesG.RemoveRange(0, Bytesperline);
                    for (int i = Xsize; i < Xsize * Ysize - Linepaddingsize; i += Xsize)
                    {
                        ImagevaluesG.RemoveRange(i, Linepaddingsize);
                    }
                    //BLUE
                    ImagevaluesB.Insert(0, 0);
                    for (int i = 0; i < Xsize * Ysize; i += Xsize)
                    {
                        ImagevaluesB.RemoveRange(i, Linepaddingsize);
                    }
                }
                else if (Nplanes == 1)
                {
                    for (int i = Xsize; i < Xsize * Ysize; i += Xsize)
                    {
                        Imagevalues.RemoveRange(i, Linepaddingsize);
                    }
                }
            }
            catch
            {
                //throw new Exception(@"Error while interpreting the data and fixing padding.");
            }
        }

        private void Convert()
        {
            try
            {
                for (int i = 0; i < Xsize * Ysize; i++)
                {
                    if (Nplanes == 1 && Paletteinfo == 2)
                    {
                        image[i] = Color.FromArgb(Imagevalues[i], Imagevalues[i], Imagevalues[i]);
                    }
                    if (Nplanes == 1 && Bitsperpixel == 1)
                    {
                        image[i] = Color.FromArgb(Imagevalues[i] * 255, Imagevalues[i] * 255, Imagevalues[i] * 255);
                    }
                    else if (Nplanes == 1 && Bitsperpixel == 2)
                    {
                        image[i] = Palette16[Imagevalues[i]];
                    }
                    else if (Nplanes == 1)
                    {
                        if (!palette256Empty)
                        {
                            image[i] = Palette256[Imagevalues[i]];
                        }
                        else if (!palette16Empty)
                        {
                            image[i] = Palette16[Imagevalues[i] / 16];
                        }
                        else
                        {
                            image[i] = Color.FromArgb(Imagevalues[i], Imagevalues[i], Imagevalues[i]);
                        }
                    }
                    else if (Nplanes == 4 && Bitsperpixel == 1)
                    {
                        image[i] = Palette16[Imagevalues[i]];
                    }
                    else if (Nplanes == 3 && Bitsperpixel == 1)
                    {
                        image[i] = Palette16[Imagevalues[i]];
                    }
                    else if (Nplanes == 3 && Paletteinfo == 1)
                    {
                        image[i] = Color.FromArgb(ImagevaluesR[i], ImagevaluesG[i], ImagevaluesB[i]);
                    }
                    int y = i / Xsize;
                    int x = i - (Xsize * y);
                    bmpImage.SetPixel(x, y, image[i]);
                }
            }
            catch
            {
                //throw new Exception(@"Error while converting.");
            }
        }

        /// <summary>
        /// Creates a new instance of the PCXImage object.
        /// </summary>
        public PCXImage()
        {
            bmpImage = null;
            Reset();
        }

        public PCXImage(string strFileName) : this()
        {
            // make sure we have a .pcx file
            if (Path.GetExtension(strFileName).ToLower() == ".pcx" || Path.GetExtension(strFileName).ToLower() == ".pcc")
            {
                bmpImage = null;
                Reset();
                // make sure the file exists
                if (File.Exists(strFileName))
                {
                    FileArray = File.ReadAllBytes(strFileName);
                    Manufacturer = FileArray[0];
                    // make sure we have a .pcx file with the right header
                    if (Manufacturer == (int)Format.DEFAULT)
                    {
                        New();
                        Header();
                        Palette();
                        Decode();
                        Fix();
                        Convert();
                        Reset();
                    }
                    else
                    {
                        throw new Exception(@"Error loading file, file '" + strFileName + "' cannot be recognized as Zsoft PCX image.");
                    }
                }
                else
                {
                    throw new Exception(@"Error loading file, file '" + strFileName + "' not found.");
                }
            }
            else
            {
                throw new Exception(@"Error loading file, file '" + strFileName + "' must have an extension of '.pcx' or 'pcc'.");
            }
        }
    }
}
