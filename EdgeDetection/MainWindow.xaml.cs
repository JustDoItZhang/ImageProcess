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

            //GaussianBlur(pixels,stride,s.PixelWidth,s.PixelHeight);
            EdgeCal(pixels, stride, s.PixelWidth, s.PixelHeight);
            


            BitmapSource newBs = BitmapSource.Create(s.PixelWidth,s.PixelHeight,s.DpiX,s.DpiY,s.Format,s.Palette, pixels, stride);
            transferedImage.Source = newBs;


        }

        void EdgeCal(byte[] pixels,int stride,int width,int height)
        {
            int[,] hOperator = new int[,]
            {
                { -1,0,1},
                { -2,0,2},
                { -1,0,1}
            };
            int[,] vOperator = new int[,]
            {
                { -1,-2,-1},
                { 0,0,0 },
                { 1,2,1 }
            };

            byte[,] orients = new byte[height, width];
            double[,] gradients = new double[height, width];
            double maxG = 0;

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

                    double orientation = 45;
                    gradients[i, j] = Math.Sqrt(gx * gx + gy * gy);
                    if (gradients[i, j] > maxG)
                        maxG = gradients[i, j];
                    if (gx == 0)
                    {
                        orientation = gy == 0 ? 0 : 90;
                    }
                    else
                    {
                        double div = gy / gx;
                        if (div < 0)
                        {
                            if(gy < 0)
                                orientation = Math.Atan(-div) * 180 / Math.PI;
                            else
                                orientation = 180 - Math.Atan(-div) * 180 / Math.PI;
                        }
                        else
                        {
                            orientation = Math.Atan(div) * 180 / Math.PI;
                        }

                        if (orientation < 22.5)
                            orientation = 0;
                        else if (orientation < 67.5)
                            orientation = 45;
                        else if (orientation < 112.5)
                            orientation = 90;
                        else if (orientation < 157.5)
                            orientation = 135;
                        else
                            orientation = 0;

                    }

                    orients[i,j] = (byte)orientation;
                }
            }


            double left = 0, right = 0;
            for (int i = 1; i < height - 1; i++)
            {
                for (int j = 1; j < width - 1; j++)
                {

                    switch (orients[i,j])
                    {
                        case 0:
                            left = gradients[i - 1, j];
                            right = gradients[i + 1, j];
                            break;
                        case 45:
                            left = gradients[i - 1, j + 1];
                            right = gradients[i + 1, j - 1];
                            break;
                        case 90:
                            left = gradients[i, j + 1];
                            right = gradients[i, j - 1];
                            break;
                        case 135:
                            left = gradients[i + 1, j + 1];
                            right = gradients[i - 1, j - 1];
                            break;
                    }

                    int start = i * stride + j * 4;
                    if (gradients[i, j] < left || gradients[i, j] < right)
                    {
                        pixels[start] = 255;
                        pixels[start + 1] = 255;
                        pixels[start + 2] = 255;
                    }
                    else
                    {
                        pixels[start] = 0;
                        pixels[start + 1] = 0;
                        pixels[start + 2] = 0;
                    }

                }
            }
        }

        double[,] GenerateGaussianRect(int radius)
        {
            double sigma = radius + 0.5d;

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
            double[,] gaussianRect = GenerateGaussianRect(2);

            for(int row = 2;row < height - 2; row ++)
            {
                for(int col = 2;col < width - 2;col ++)
                {
                    double r = 0, g = 0, b = 0;
                    for(int rs = -2;rs <= 2;rs ++)
                    {
                        for (int cs = -2; cs <= 2; cs++)
                        {
                            b += source[(row + rs) * stride + (col + cs) * 4] * gaussianRect[(2 + rs), 2 + cs];
                            g += source[(row + rs) * stride + (col + cs) * 4 + 1] * gaussianRect[(2 + rs), 2 + cs];
                            r += source[(row + rs) * stride + (col + cs) * 4 + 2] * gaussianRect[(2 + rs), 2 + cs];
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
