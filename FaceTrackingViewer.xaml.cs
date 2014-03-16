// --------------------------------------------------------------------------------------------------------------------
// <copyright file="FaceTrackingViewer.xaml.cs" company="Microsoft">
//     Copyright (c) Microsoft Corporation.  All rights reserved.
// </copyright>
// --------------------------------------------------------------------------------------------------------------------

namespace FaceTrackingandVARecord
{
    using System;
    using System.IO;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Windows;
    using System.Windows.Controls;
    using System.Windows.Media;
    using Microsoft.Kinect;
    using Microsoft.Kinect.Toolkit.FaceTracking;

    using Point = System.Windows.Point;

    /// <summary>
    /// Class that uses the Face Tracking SDK to display a face mask for
    /// tracked skeletons
    /// </summary>
    public partial class FaceTrackingViewer : UserControl, IDisposable
    {
        public static readonly DependencyProperty KinectProperty = DependencyProperty.Register(
            "Kinect", 
            typeof(KinectSensor), 
            typeof(FaceTrackingViewer), 
            new PropertyMetadata(
                null, (o, args) => ((FaceTrackingViewer)o).OnSensorChanged((KinectSensor)args.OldValue, (KinectSensor)args.NewValue)));

        private const uint MaxMissedFrames = 100;

        private readonly Dictionary<int, SkeletonFaceTracker> trackedSkeletons = new Dictionary<int, SkeletonFaceTracker>();

        private byte[] colorImage;

        private ColorImageFormat colorImageFormat = ColorImageFormat.Undefined;

        private short[] depthImage;

        private DepthImageFormat depthImageFormat = DepthImageFormat.Undefined;

        private bool disposed;

        private Skeleton[] skeletonData;

        

        public FaceTrackingViewer()
        {
            this.InitializeComponent();
        }

        ~FaceTrackingViewer()
        {
            this.Dispose(false);
        }

        public KinectSensor Kinect
        {
            get
            {
                return (KinectSensor)this.GetValue(KinectProperty);
            }

            set
            {
                this.SetValue(KinectProperty, value);
            }
        }

        public void Dispose()
        {
            this.Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!this.disposed)
            {
                this.ResetFaceTracking();

                this.disposed = true;
            }
        }

        protected override void OnRender(DrawingContext drawingContext)
        {
            if (MainWindow.vMode != 0)
            {
            base.OnRender(drawingContext);
            
                    foreach (SkeletonFaceTracker faceInformation in this.trackedSkeletons.Values)
                    {
                        faceInformation.DrawFaceModel(drawingContext);
                    }
             }
           
        }

        private void OnAllFramesReady(object sender, AllFramesReadyEventArgs allFramesReadyEventArgs)
        {
            ColorImageFrame colorImageFrame = null;
            DepthImageFrame depthImageFrame = null;
            SkeletonFrame skeletonFrame = null;

            try
            {
                colorImageFrame = allFramesReadyEventArgs.OpenColorImageFrame();
                depthImageFrame = allFramesReadyEventArgs.OpenDepthImageFrame();
                skeletonFrame = allFramesReadyEventArgs.OpenSkeletonFrame();

                if (colorImageFrame == null || depthImageFrame == null || skeletonFrame == null)
                {
                    return;
                }

                // Check for image format changes.  The FaceTracker doesn't
                // deal with that so we need to reset.
                if (this.depthImageFormat != depthImageFrame.Format)
                {
                    this.ResetFaceTracking();
                    this.depthImage = null;
                    this.depthImageFormat = depthImageFrame.Format;
                }

                if (this.colorImageFormat != colorImageFrame.Format)
                {
                    this.ResetFaceTracking();
                    this.colorImage = null;
                    this.colorImageFormat = colorImageFrame.Format;
                }

                // Create any buffers to store copies of the data we work with
                if (this.depthImage == null)
                {
                    this.depthImage = new short[depthImageFrame.PixelDataLength];
                }

                if (this.colorImage == null)
                {
                    this.colorImage = new byte[colorImageFrame.PixelDataLength];
                }
                
                // Get the skeleton information
                if (this.skeletonData == null || this.skeletonData.Length != skeletonFrame.SkeletonArrayLength)
                {
                    this.skeletonData = new Skeleton[skeletonFrame.SkeletonArrayLength];
                }

                colorImageFrame.CopyPixelDataTo(this.colorImage);
                depthImageFrame.CopyPixelDataTo(this.depthImage);
                skeletonFrame.CopySkeletonDataTo(this.skeletonData);
                
                //record video                
                if ((RecorderHelper.IsRecording) && (RecorderHelper.VideoROn))                
                {
                    RecorderHelper.bm = RecorderHelper.ImageToBitmap(colorImageFrame);
                        if (RecorderHelper.aviStream == null)
                    {
                        RecorderHelper.aviStream = RecorderHelper.aviManager.AddVideoStream(false, 30, RecorderHelper.bm);
                    }
                        else
                    {
                        RecorderHelper.aviStream.AddFrame(RecorderHelper.bm);
                    }
                        RecorderHelper.bm.Dispose();
                }
                 

                // Update the list of trackers and the trackers with the current frame information
                foreach (Skeleton skeleton in this.skeletonData)
                {
                    if (skeleton.TrackingState == SkeletonTrackingState.Tracked
                        || skeleton.TrackingState == SkeletonTrackingState.PositionOnly)
                    {
                        // We want keep a record of any skeleton, tracked or untracked.
                        if (!this.trackedSkeletons.ContainsKey(skeleton.TrackingId))
                        {
                            this.trackedSkeletons.Add(skeleton.TrackingId, new SkeletonFaceTracker());
                        }

                        // Give each tracker the upated frame.
                        SkeletonFaceTracker skeletonFaceTracker;
                        if (this.trackedSkeletons.TryGetValue(skeleton.TrackingId, out skeletonFaceTracker))
                        {
                            skeletonFaceTracker.OnFrameReady(this.Kinect, colorImageFormat, colorImage, depthImageFormat, depthImage, skeleton);
                            skeletonFaceTracker.LastTrackedFrame = skeletonFrame.FrameNumber;
                        }
                    }
                }

                this.RemoveOldTrackers(skeletonFrame.FrameNumber);

                this.InvalidateVisual();
            }
            finally
            {
                if (colorImageFrame != null)
                {
                    colorImageFrame.Dispose();
                }

                if (depthImageFrame != null)
                {
                    depthImageFrame.Dispose();
                }

                if (skeletonFrame != null)
                {
                    skeletonFrame.Dispose();
                }
            }
        }

        private void OnSensorChanged(KinectSensor oldSensor, KinectSensor newSensor)
        {
            if (oldSensor != null)
            {
                oldSensor.AllFramesReady -= this.OnAllFramesReady;
                this.ResetFaceTracking();
            }

            if (newSensor != null)
            {
                newSensor.AllFramesReady += this.OnAllFramesReady;
            }
        }

        /// <summary>
        /// Clear out any trackers for skeletons we haven't heard from for a while
        /// </summary>
        private void RemoveOldTrackers(int currentFrameNumber)
        {
            var trackersToRemove = new List<int>();

            foreach (var tracker in this.trackedSkeletons)
            {
                uint missedFrames = (uint)currentFrameNumber - (uint)tracker.Value.LastTrackedFrame;
                if (missedFrames > MaxMissedFrames)
                {
                    // There have been too many frames since we last saw this skeleton
                    trackersToRemove.Add(tracker.Key);
                }
            }

            foreach (int trackingId in trackersToRemove)
            {
                this.RemoveTracker(trackingId);
            }
        }

        private void RemoveTracker(int trackingId)
        {
            this.trackedSkeletons[trackingId].Dispose();
            this.trackedSkeletons.Remove(trackingId);
        }

        private void ResetFaceTracking()
        {
            foreach (int trackingId in new List<int>(this.trackedSkeletons.Keys))
            {
                this.RemoveTracker(trackingId);
            }
        }

        private class SkeletonFaceTracker : IDisposable
        {
            private static FaceTriangle[] faceTriangles;

            private EnumIndexableCollection<FeaturePoint, PointF> facePoints;

            private FaceTracker faceTracker;

            private bool lastFaceTrackSucceeded;

            private SkeletonTrackingState skeletonTrackingState;

            public int LastTrackedFrame { get; set; }

            public void Dispose()
            {
                if (this.faceTracker != null)
                {
                    this.faceTracker.Dispose();
                    this.faceTracker = null;
                }
            }

            public void DrawFaceModel(DrawingContext drawingContext)
            {
                if (!this.lastFaceTrackSucceeded || this.skeletonTrackingState != SkeletonTrackingState.Tracked)
                {
                    return;
                }

                

                var faceModelPts = new List<Point>();
                var faceModel = new List<FaceModelTriangle>();

                var brushThick=1.0;
                var faceModelGroup = new GeometryGroup();

                if (MainWindow.vMode == 0)
                {
                    return;
                }
                else if (MainWindow.vMode == 1)
                {
                    //POINTS
                    brushThick = 5.0;

                    Point point;
                    int Count = 87;
                    for (int i = 0; i < Count; i++)
                    {
                        point = new Point(this.facePoints[i].X + 0.5f, this.facePoints[i].Y + 0.5f);
                        //faceModelPts.Add(new Point(this.facePoints[i].X + 0.5f, this.facePoints[i].Y + 0.5f));
                        faceModelGroup.Children.Add(new EllipseGeometry(point, 0.1, 0.1));
                    }
                    //
                }
                else if (MainWindow.vMode == 2)
                {

                    for (int i = 0; i < this.facePoints.Count; i++)
                    {
                        faceModelPts.Add(new Point(this.facePoints[i].X + 0.5f, this.facePoints[i].Y + 0.5f));
                    }

                    foreach (var t in faceTriangles)
                    {
                        var triangle = new FaceModelTriangle();
                        triangle.P1 = faceModelPts[t.First];
                        triangle.P2 = faceModelPts[t.Second];
                        triangle.P3 = faceModelPts[t.Third];
                        faceModel.Add(triangle);
                    }
               
                    for (int i = 0; i < faceModel.Count; i++)
                    {
                        var faceTriangle = new GeometryGroup();
                        faceTriangle.Children.Add(new LineGeometry(faceModel[i].P1, faceModel[i].P2));
                        faceTriangle.Children.Add(new LineGeometry(faceModel[i].P2, faceModel[i].P3));
                        faceTriangle.Children.Add(new LineGeometry(faceModel[i].P3, faceModel[i].P1));
                        faceModelGroup.Children.Add(faceTriangle);
                    }
                }               
                drawingContext.DrawGeometry(Brushes.LightYellow, new Pen(Brushes.LightYellow, brushThick), faceModelGroup);
                
            }

            /// <summary>
            /// Updates the face tracking information for this skeleton
            /// </summary>
            internal void OnFrameReady(KinectSensor kinectSensor, ColorImageFormat colorImageFormat, byte[] colorImage, DepthImageFormat depthImageFormat, short[] depthImage, Skeleton skeletonOfInterest)
            {
                
                Image image1=null;

                try{
                    var tSting = Application.Current.MainWindow.FindName("image1");
                    image1 = tSting as Image;
                    image1.Visibility = System.Windows.Visibility.Visible;
                    }
                catch (NullReferenceException e)
                {
                    //throw e;    // Rethrowing exception e
                }

     
                
                //var tSting=Application.Current.MainWindow.FindName("image1");
                //Image image1 = tSting as Image;
                //image1.Visibility = System.Windows.Visibility.Visible;
                this.skeletonTrackingState = skeletonOfInterest.TrackingState;

                if (this.skeletonTrackingState != SkeletonTrackingState.Tracked)
                {
                    // nothing to do with an untracked skeleton.
                    return;
                }

                if (this.faceTracker == null)
                {
                    try
                    {
                        this.faceTracker = new FaceTracker(kinectSensor);
                    }
                    catch (InvalidOperationException)
                    {
                        // During some shutdown scenarios the FaceTracker
                        // is unable to be instantiated.  Catch that exception
                        // and don't track a face.
                        Debug.WriteLine("AllFramesReady - creating a new FaceTracker threw an InvalidOperationException");
                        this.faceTracker = null;
                    }
                }

                if (this.faceTracker != null)
                {
                    FaceTrackFrame frame = this.faceTracker.Track(
                        colorImageFormat, colorImage, depthImageFormat, depthImage, skeletonOfInterest);

                    this.lastFaceTrackSucceeded = frame.TrackSuccessful;
                    if (this.lastFaceTrackSucceeded)
                    {

                        if (image1 != null)
                        {
                            image1.Visibility = System.Windows.Visibility.Hidden;
                        }
                        
                        //get au
                        if (RecorderHelper.IsRecording == true)
                        {
                            var AU = frame.GetAnimationUnitCoefficients();
                            /*var jL = AU[AnimationUnit.JawLower];
                            var lCD = AU[AnimationUnit.LipCornerDepressor];
                            var lR = AU[AnimationUnit.LipRaiser];
                            var lS = AU[AnimationUnit.LipStretcher];
                            var bL = AU[AnimationUnit.BrowLower];
                            var bS = AU[AnimationUnit.BrowRaiser];*/
                            //StreamWriter auRecord = new StreamWriter("C:\\AU.txt");
                            /*RecorderHelper.auRecord.WriteLine(DateTime.Now.ToString()+":"+DateTime.Now.Millisecond.ToString()+":");
                            RecorderHelper.auRecord.WriteLine(AU[AnimationUnit.JawLower]);
                            RecorderHelper.auRecord.WriteLine(AU[AnimationUnit.LipRaiser]);
                            RecorderHelper.auRecord.WriteLine(AU[AnimationUnit.LipCornerDepressor]);                            
                            RecorderHelper.auRecord.WriteLine(AU[AnimationUnit.LipStretcher]);
                            RecorderHelper.auRecord.WriteLine(AU[AnimationUnit.BrowRaiser]);
                            RecorderHelper.auRecord.WriteLine(AU[AnimationUnit.BrowLower]);*/
                            RecorderHelper.auRecord.WriteLine(AU[AnimationUnit.JawLower] + " " + AU[AnimationUnit.LipRaiser] + " " + AU[AnimationUnit.LipCornerDepressor] + " " + AU[AnimationUnit.LipStretcher] + " " + AU[AnimationUnit.BrowRaiser] + " " + AU[AnimationUnit.BrowLower]);

                            //auRecord.Close();
                        }
                        

                        if (faceTriangles == null)
                        {
                            // only need to get this once.  It doesn't change.
                            faceTriangles = frame.GetTriangles();
                        }

                        this.facePoints = frame.GetProjected3DShape();
                    }
                }
            }

            private struct FaceModelTriangle
            {
                public Point P1;
                public Point P2;
                public Point P3;
            }
        }
    }
}