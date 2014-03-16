using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using Microsoft.Kinect;

namespace FaceTrackingandVARecord
{
    public class RecordVideo
    {

        public static Queue<ColorImageFrame> dataQueue = new Queue<ColorImageFrame>();
        public static ReaderWriterLock rw = new ReaderWriterLock();
        
        public static void AddDocument(ColorImageFrame data)
        {
                rw.AcquireWriterLock(100);         
                dataQueue.Enqueue(data);
                rw.ReleaseWriterLock();

            
        }

        
        //public static void ColorImageFrame GetDocument()
        public static void GetDocument()
        {
                rw.AcquireWriterLock(50);
                if (dataQueue.Count > 0)
                {
                    ColorImageFrame data = null;
                    data = dataQueue.Dequeue();
                }
                rw.ReleaseWriterLock();
                //return data;
            
        }
        
        public static bool IsDocumentAvailable
        {
            get
            {
                return dataQueue.Count > 0;
            }
        }

    }
}
