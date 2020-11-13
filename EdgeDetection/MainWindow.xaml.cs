using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace EdgeDetection
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            
        }

        private void transfer_Click(object sender, RoutedEventArgs e)
        {
            BitmapSource s = sourceImage.Source as BitmapSource;

            int stride = s.PixelWidth * s.Format.BitsPerPixel / 8;
            byte[] pixels = new byte[s.PixelHeight * stride];
            s.CopyPixels(pixels, stride, 0);


            GaussianBlur(pixels,stride,s.PixelWidth,s.PixelHeight);
            EdgeCal(pixels, stride, s.PixelWidth, s.PixelHeight);
            


            BitmapSource newBs = BitmapSource.Create(s.PixelWidth,s.PixelHeight,s.DpiX,s.DpiY,s.Format,s.Palette, pixels, stride);
            transferedImage.Source = newBs;


        }

        void EdgeCal(byte[] pixels, int stride, int width, int height)
        {
            int[,] hOperator = new int[,]
            {
                { -1,0,1},
                { -2,0,2},
                { -1,0,1}
            };
            int[,] vOperator = new int[,]
            {
                { 1,2,1},
                { 0,0,0 },
                { -1,-2,-1 }
            };


            byte[,] orients = new byte[height, width];
            double[,] grandx = new double[height, width];
            double[,] grandy = new double[height, width];
            double[,] gradients = new double[height, width];
            double maxGra = 0;

            for (int i = 1; i < height - 1; i++)
            {
                for (int j = 1; j < width - 1; j++)
                {
                    double gx = 0;
                    double gy = 0;

                    for (int h = -1; h <= 1; h++)
                    {
                        for (int v = -1; v <= 1; v++)
                        {
                            int nstart = (i + h) * stride + (v + j) * 4;
                            double gray = (pixels[nstart + 2] * 0.3 + pixels[nstart + 1] * 0.59 + pixels[nstart] * 0.11);

                            double x = gray * hOperator[h + 1, v + 1];
                            double y = gray * vOperator[h + 1, v + 1];
                            gx += x;
                            gy += y;
                        }
                    }


                    grandx[i, j] = gx;
                    grandy[i, j] = gy;
                    gradients[i, j] = Math.Sqrt(gx * gx + gy * gy);
                    if (gradients[i, j] > maxGra)
                        maxGra = gradients[i, j];

                    double orientation = 0;

                    orientation = Math.Atan2(gx, gy) * 180 / Math.PI;
                    if (orientation >= 0 && orientation < 45)
                        orientation = 0;
                    else if (orientation < 90 && orientation >= 45)
                        orientation = 1;
                    else if (orientation < 135 && orientation >= 90)
                        orientation = 2;
                    else if (orientation < 180 && orientation >= 135)
                        orientation = 3;
                    else if (orientation < 0 && orientation >= -45)
                        orientation = 3;
                    else if (orientation < -45 && orientation >= -90)
                        orientation = 2;
                    else if (orientation < -90 && orientation > -135)
                        orientation = 1;
                    else
                        orientation = 0;

                    orients[i, j] = (byte)orientation;
                }
            }

            //64个等级直方图信息
            int[] hists = new int[64];

            for (int i = 1; i < height - 1; i++)
            {
                for (int j = 1; j < width - 1; j++)
                {
                    gradients[i, j] = gradients[i, j] / maxGra;

                    byte index = (byte)(gradients[i, j] * 64);
                    hists[index == 64 ? 63 : index] ++;
                }
            }

            double high = 0;
            double low = 0;
            for(int i = 0;i < 64;i ++)
            {
                if (i > 0)
                    hists[i] += hists[i - 1];
                if(hists[i] > 0.8 * height * width)
                {
                    high = i / 64d;
                    break;
                }
            }
            low = 0.4 * high;

            double[,] grays = new double[height, width];
            double left = 0, right = 0;
            for (int i = 1; i < height - 1; i++)
            {
                for (int j = 1; j < width - 1; j++)
                {
                    double d = 0;
                    double E = gradients[i, j + 1];
                    double NE = gradients[i - 1, j + 1];
                    double N = gradients[i - 1, j];
                    double NW = gradients[i - 1, j - 1];
                    double W = gradients[i, j - 1];
                    double SW = gradients[i + 1, j - 1];
                    double S = gradients[i + 1, j];
                    double SE = gradients[i + 1, j + 1];
                    switch (orients[i, j])
                    {
                        case 0:
                            d = Math.Abs(grandy[i, j] / grandx[i, j]);
                            left = SW * d + W * (1 - d);
                            right = NE * d + E * (1 - d);
                            break;
                        case 1:
                            d = Math.Abs(grandx[i, j] / grandy[i, j]);
                            left = SW * d + S * (1 - d);
                            right = NE * d + N * (1 - d);
                            break;
                        case 2:
                            d = Math.Abs(grandx[i, j] / grandy[i, j]);
                            left = NW * d + N * (1 - d);
                            right = SE * d + S * (1 - d);
                            break;
                        case 3:
                            d = Math.Abs(grandy[i, j] / grandx[i, j]);
                            left = NW * d + W * (1 - d);
                            right = SE * d + E * (1 - d);
                            break;
                        case 254:
                            left = S;
                            right = N;
                            break;
                        case 255:
                            left = W;
                            right = E;
                            break;
                    }


                    if (gradients[i, j] >= left && gradients[i, j] >= right)
                    {
                        if (gradients[i, j] > high)
                        {
                            grays[i, j] = 1;
                        }
                        else if (gradients[i, j] > low )
                        {
                            grays[i, j] = 0.4;
                        }
                        else
                        {
                            grays[i, j] = 0;
                        }
                    }
                    else
                    {
                        grays[i, j] = 0;
                    }
                }
            }

            byte[,] newGrays = new byte[height, width];
            for (int i = 1; i < height - 1; i++)
            {
                for (int j = 1; j < width - 1; j++)
                {
                    if (grays[i, j] > 0 && grays[i, j] < 1)
                    {
                        for (int x = -1; x <= 1; x++)
                        {
                            for (int y = -1; y <= 1; y++)
                            {
                                if (grays[i + x, j + x] == 1 && grays[i, j] > 0)
                                {
                                    newGrays[i, j] = 1;
                                }
                            }
                        }

                        if (newGrays[i, j] < 1) newGrays[i, j] = 0;
                    }
                    else
                    {
                        newGrays[i, j] = (byte)grays[i, j];
                    }
                }
            }

            //using (FileStream fs = File.OpenWrite("data.txt"))
            //{
            //    using (StreamWriter sw = new StreamWriter(fs))
            //    {
            //        for (int i = 0; i < height; i++)
            //        {
            //            for (int j = 0; j < width; j++)
            //            {
            //                sw.Write(gradients[i, j] + " ");
            //            }
            //            sw.WriteLine();
            //        }
            //    }
            //}

            for (int i = 0; i < height; i++)
            {
                for (int j = 0; j < width; j++)
                {
                    int start = i * stride + j * 4;
                    pixels[start] = pixels[start + 1] = pixels[start + 2] = (byte)(newGrays[i, j] * 255);
                }
            }
        }

        double[,] GenerateGaussianRect(int radius)
        {
            double sigma = radius / 2 + 0.5d;

            double[,] dest = new double[radius * 2 + 1, radius * 2 + 1];
            double total = 0;
            for(int i = 0;i < radius * 2 + 1;i ++)
            {
                for (int j = 0; j < radius * 2 + 1; j++)
                {
                    double val = 1 / (2 * Math.PI * sigma) * Math.Exp(-((i - 2) * (i - 2) + (j - 2) * (j - 2)) / (2 * sigma * sigma));
                    dest[i, j] = val;
                    total += val;
                }
            }

            for (int i = 0; i < radius * 2 + 1; i++)
            {
                for (int j = 0; j < radius * 2 + 1; j++)
                {
                    dest[i, j] = dest[i, j] / total;
                }
            }
            return dest;
        }

        void GaussianBlur(byte[] source,int stride,int width,int height)
        {
            int radius = 2;
            double[,] gaussianRect = GenerateGaussianRect(radius);

            for (int row = radius; row < height - radius; row ++)
            {
                for(int col = radius; col < width - radius; col ++)
                {
                    double r = 0, g = 0, b = 0;
                    for(int rs = -radius; rs <= radius; rs ++)
                    {
                        for (int cs = -radius; cs <= radius; cs++)
                        {
                            b += source[(row + rs) * stride + (col + cs) * 4] * gaussianRect[(radius + rs), radius + cs];
                            g += source[(row + rs) * stride + (col + cs) * 4 + 1] * gaussianRect[(radius + rs), radius + cs];
                            r += source[(row + rs) * stride + (col + cs) * 4 + 2] * gaussianRect[(radius + rs), radius + cs];
                        }
                    }

                    source[row * stride + col * 4] = (byte)b;
                    source[row * stride + col * 4 + 1] = (byte)g;
                    source[row * stride + col * 4 + 2] = (byte)r;
                }
            }
        }
    }
}
