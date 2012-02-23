using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using Microsoft.Kinect;

namespace KinectSample
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

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            kinectSensorChooser.KinectSensorChanged += new DependencyPropertyChangedEventHandler(kinectSensorChooser_KinectSensorChanged);
        }

        private void kinectSensorChooser_KinectSensorChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            // Stop old senson   
            KinectSensor oldSensor = (KinectSensor)e.OldValue;
            kinectStop(oldSensor);

            // Get the new sensor
            KinectSensor newSensor = (KinectSensor)e.NewValue;
            if (newSensor == null)
            {
                return;
            }

            // Get the sensor up-and-running
            newSensor.AllFramesReady += new EventHandler<AllFramesReadyEventArgs>(newSensort_AllFramesReady);
            newSensor.ColorStream.Enable(ColorImageFormat.RgbResolution640x480Fps30);
            newSensor.DepthStream.Enable(DepthImageFormat.Resolution640x480Fps30);
            newSensor.SkeletonStream.Enable();

            try
            {
                newSensor.Start();
            }
            catch (System.IO.IOException)
            {
                kinectSensorChooser.AppConflictOccurred();
            }

            lblCurrentAngle.Content = kinectSensorChooser.Kinect.ElevationAngle.ToString();
            slider1.Value = kinectSensorChooser.Kinect.ElevationAngle;
        }

        private void newSensort_AllFramesReady(object sender, AllFramesReadyEventArgs e)
        {
            kinectUpdateColorImage(e.OpenColorImageFrame());
            kinectUpdateDepthImage(e.OpenDepthImageFrame());
            kinectPlayerImage(e.OpenColorImageFrame(), e.OpenDepthImageFrame());
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            kinectStop(kinectSensorChooser.Kinect);
        }

        private void kinectStop(KinectSensor sensor)
        {
            if (sensor != null)
            {
                // Stop the sensor
                sensor.Stop();

                // Stop audio
                if (sensor.AudioSource != null) 
                {
                    sensor.AudioSource.Stop();
                }
            }
        }

        # region Depht image processing

        const float MaxDepthDistance = 4095; // max value returned
        const float MinDepthDistance = 850; // min value returned
        const float MaxDepthDistanceOffset = MaxDepthDistance - MinDepthDistance;

        private static WriteableBitmap wbitmapDepth = new WriteableBitmap(640, 480, 96, 96, PixelFormats.Bgr32, null);
        private void kinectUpdateDepthImage(DepthImageFrame depthFrame)
        {
            if (depthFrame == null) { return; }

            byte[] pixels = GenerateColoredBytes(depthFrame);

            // Update the image
            int stride = depthFrame.Width * 4; // (B,G,R,Empty)
            wbitmapDepth.WritePixels(new Int32Rect(0, 0, wbitmapDepth.PixelWidth, wbitmapDepth.PixelHeight), pixels, stride, 0);
            depthImage.Source = wbitmapDepth;
        }

        private byte[] GenerateColoredBytes(DepthImageFrame depthFrame)
        {
            // Get the raw data from kinect with the depth for every pixel
            short[] rawDepthData = new short[depthFrame.PixelDataLength];
            depthFrame.CopyPixelDataTo(rawDepthData); 

            //use depthFrame to create the image to display on-screen
            //depthFrame contains color information for all pixels in image
            //Height x Width x 4 (Red, Green, Blue, empty byte)
            Byte[] pixels = new byte[depthFrame.Height * depthFrame.Width * 4];

            //Bgr32  - Blue, Green, Red, empty byte
            //Bgra32 - Blue, Green, Red, transparency 
            //You must set transparency for Bgra as .NET defaults a byte to 0 = fully transparent

            //hardcoded locations to Blue, Green, Red (BGR) index positions       
            const int BlueIndex = 0;
            const int GreenIndex = 1;
            const int RedIndex = 2;

            
            //loop through all distances
            //pick a RGB color based on distance
            for (int depthIndex = 0, colorIndex = 0; 
                depthIndex < rawDepthData.Length && colorIndex < pixels.Length; 
                depthIndex++, colorIndex += 4)
            {
                //get the player (requires skeleton tracking enabled for values)
                int player = rawDepthData[depthIndex] & DepthImageFrame.PlayerIndexBitmask;

                //gets the depth value
                int depth = rawDepthData[depthIndex] >> DepthImageFrame.PlayerIndexBitmaskWidth;

                //.9M or 2.95'
                if (depth <= 900)
                {
                    //we are very close
                    pixels[colorIndex + BlueIndex] = 255;
                    pixels[colorIndex + GreenIndex] = 0;
                    pixels[colorIndex + RedIndex] = 0;

                }
                // .9M - 2M or 2.95' - 6.56'
                else if (depth > 900 && depth < 2000)
                {
                    //we are a bit further away
                    pixels[colorIndex + BlueIndex] = 0;
                    pixels[colorIndex + GreenIndex] = 255;
                    pixels[colorIndex + RedIndex] = 0;
                }
                // 2M+ or 6.56'+
                else if (depth > 2000)
                {
                    //we are the farthest
                    pixels[colorIndex + BlueIndex] = 0;
                    pixels[colorIndex + GreenIndex] = 0;
                    pixels[colorIndex + RedIndex] = 255;
                }


                ////equal coloring for monochromatic histogram
                //byte intensity = CalculateIntensityFromDepth(depth);
                //pixels[colorIndex + BlueIndex] = intensity;
                //pixels[colorIndex + GreenIndex] = intensity;
                //pixels[colorIndex + RedIndex] = intensity;


                //Color all players "gold"
                if (player > 0)
                {
                    pixels[colorIndex + BlueIndex] = 0;
                    pixels[colorIndex + GreenIndex] = 0;
                    pixels[colorIndex + RedIndex] = 0;
                }

            }
          

            return pixels;
        }


        public static byte CalculateIntensityFromDepth(int distance)
        {
            // Formula for calculating monochrome intensity for histogram
            return (byte)(255 - (255 * Math.Max(distance - MinDepthDistance, 0) / (MaxDepthDistanceOffset)));
        }

        # endregion

        # region Color image processing
        private static WriteableBitmap wbitmapColor = new WriteableBitmap(640, 480, 96, 96, PixelFormats.Bgr32, null);
        private void kinectUpdateColorImage(ColorImageFrame colorFrame)
        {
            if (colorFrame == null) { return; }

            // Get pixel values
            byte[] pixels = new Byte[colorFrame.PixelDataLength];
            colorFrame.CopyPixelDataTo(pixels);
            
            // Update the image
            int stride = colorFrame.Width * 4;
            wbitmapColor.WritePixels(new Int32Rect(0, 0, wbitmapColor.PixelWidth, wbitmapColor.PixelHeight), pixels, stride, 0);
            colorImage.Source = wbitmapColor;
        }
        # endregion

        # region Tilt handler
        /**
         * Handle the tilt events, when slider is moved. 
         */
        private void slider1_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            slider1.IsEnabled = false;

            // Set angle to slider value
            if (kinectSensorChooser.Kinect != null && kinectSensorChooser.Kinect.IsRunning)
            {
                kinectSensorChooser.Kinect.ElevationAngle = (int)slider1.Value;
                lblCurrentAngle.Content = kinectSensorChooser.Kinect.ElevationAngle;
            }

            System.Threading.Thread.Sleep(new TimeSpan(hours: 0, minutes: 0, seconds: 1));
            slider1.IsEnabled = true;
        }
        #endregion

        private static WriteableBitmap playerBitmap = new WriteableBitmap(640, 480, 96, 96, PixelFormats.Bgra32, null);
        private void kinectPlayerImage(ColorImageFrame colorFrame, DepthImageFrame depthFrame)
        {
            if (colorFrame == null || depthFrame == null) { return;  }

            // Image color index
            const int BlueIndex = 0;
            const int GreenIndex = 1;
            const int RedIndex = 2;
            const int AlphaIndex = 3;

            // Get color image
            byte[] colorPixels = new Byte[colorFrame.PixelDataLength];
            colorFrame.CopyPixelDataTo(colorPixels);

            // Get depth image
            short[] rawDepthData = new short[depthFrame.PixelDataLength];
            depthFrame.CopyPixelDataTo(rawDepthData);

            // Create array to hold depth mapping data.
            ColorImagePoint[] _mappedDepthLocations = new ColorImagePoint[depthFrame.PixelDataLength];

            // Each index in depth array is equal to 4 pixels in color array (B, G, R, A)
            for (int depthIndex = 0, colorIndex = 0, x = 0;
                depthIndex < rawDepthData.Length && colorIndex < colorPixels.Length;
                depthIndex++, colorIndex += 4)
            {
                if (depthIndex % depthFrame.Width == 0)
                {
                    x++;
                }

                // Get the player (requires skeleton tracking enabled for values)
                int player = rawDepthData[depthIndex] & DepthImageFrame.PlayerIndexBitmask;
                if (player <= 0)
                {
                    // Not a player
                    int y = (x * depthFrame.Width) - depthIndex;
                    ColorImagePoint point = depthFrame.MapToColorImagePoint(x, y, ColorImageFormat.RgbResolution640x480Fps30);

                    colorPixels[colorIndex + BlueIndex] = 0;
                    colorPixels[colorIndex + GreenIndex] = 0;
                    colorPixels[colorIndex + RedIndex] = 0;//(byte)((colorPixels[colorIndex] + 255) >> 1);
                    colorPixels[colorIndex + AlphaIndex] = 0;
                }
                else
                {
                    colorPixels[colorIndex + AlphaIndex] = 255;
                }
            }

            // Update the image
            int stride = colorFrame.Width * 4; // (B,G,R,Empty)
            playerBitmap.WritePixels(new Int32Rect(0, 0, playerBitmap.PixelWidth, playerBitmap.PixelHeight), colorPixels, stride, 0);
            playerImage.Source = playerBitmap;
        }
    }
}
