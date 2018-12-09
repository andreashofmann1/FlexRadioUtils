using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FlexUtils
{
    public sealed class Events
    {
        public class RadioContextChangedEventArgs : EventArgs
        {
            public int CurrentTXPower { get; set; }
            public string CurrentBand { get; set; }
            public string CurrentTX { get; set; }
            public bool FirstEvent { get; set; }
        }

        public delegate void RadioContextChangedEventHandler(RadioContextChangedEventArgs e);
        public static event RadioContextChangedEventHandler RadioContextChanged;

        public static void OnRadioContextChanged(RadioContextChangedEventArgs e)
        {
            if (RadioContextChanged != null)
                RadioContextChanged(e);
        }


        public class RadioAddedEventArgs : EventArgs
        {
            public string Description { get; set; }
        }

        public delegate void RadioAddedEventHandler(RadioAddedEventArgs e);
        public static event RadioAddedEventHandler RadioAdded;

        public static void OnRadioAdded(RadioAddedEventArgs e)
        {
            if (RadioAdded != null)
                RadioAdded(e);
        }
    }
}
