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
            KinectStop(oldSensor);

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
        }

        void newSensort_AllFramesReady(object sender, AllFramesReadyEventArgs e)
        {
            using(ColorImageFrame colorFrame = e.OpenColorImageFrame()) 
            {
                if (colorFrame == null) { return; }
                byte[] pixels = new Byte[colorFrame.PixelDataLength];
                colorFrame.CopyPixelDataTo(pixels);

                WriteableBitmap wbitmap = new WriteableBitmap(colorFrame.Width, colorFrame.Height, 96, 96, PixelFormats.Bgr32, null);
                int stride = colorFrame.Width * 4;
                wbitmap.WritePixels(new Int32Rect(0, 0, wbitmap.PixelWidth, wbitmap.PixelHeight), pixels, stride, 0);
                colorImage.Source = wbitmap;
            }
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {

        }

        private void KinectStop(KinectSensor sensor)
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
    }
}
