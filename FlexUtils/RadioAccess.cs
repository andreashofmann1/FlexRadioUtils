using Flex.Smoothlake.FlexLib;
using System;
using System.ComponentModel;

namespace FlexUtils
{
    public sealed class RadioAccess
    {
        private static readonly Lazy<RadioAccess> lazy = new Lazy<RadioAccess>(() => new RadioAccess());

        internal static RadioAccess Instance { get { return lazy.Value; } }

        private Radio currentRadio;
        private bool firstConnection = true;

        private RadioAccess()
        {
            API.ProgramName = "FlexUtils";
            API.RadioAdded += new API.RadioAddedEventHandler(API_RadioAdded);
            API.RadioRemoved += new API.RadioRemovedEventHandler(API_RadioRemoved);
            API.Init();
        }

        private void API_RadioAdded(Radio radio)
        {
            if (currentRadio == null)
            {
                currentRadio = radio;
                currentRadio.Connect();

                currentRadio.PropertyChanged += new PropertyChangedEventHandler(Radio_PropertyChanged);
                currentRadio.SliceAdded += new Radio.SliceAddedEventHandler(Radio_SliceAdded);
                currentRadio.SliceRemoved += new Radio.SliceRemovedEventHandler(Radio_SliceRemoved);

                Events.OnRadioAdded(new Events.RadioAddedEventArgs() { Description = string.Format("{0} - {1}", currentRadio.Model, currentRadio.Nickname) });
                OnRadioContextChanged();
            }
        }

        private void API_RadioRemoved(Radio radio)
        {
            if (radio == currentRadio)
                currentRadio = null;
        }


        private void Radio_SliceAdded(Slice s)
        {
            OnRadioContextChanged();
        }

        private void Radio_SliceRemoved(Slice s)
        {
        }

        private void OnRadioContextChanged()
        {
            if (currentRadio.TransmitSlice != null && currentRadio.TransmitSlice.Panadapter != null 
                && !string.IsNullOrWhiteSpace(currentRadio.TransmitSlice.Panadapter.Band) && !string.IsNullOrWhiteSpace(currentRadio.TransmitSlice.TXAnt))
            {
                Events.OnRadioContextChanged(new Events.RadioContextChangedEventArgs()
                {
                    CurrentBand = currentRadio.TransmitSlice.Panadapter.Band,
                    CurrentTX = currentRadio.TransmitSlice.TXAnt,
                    CurrentTXPower = currentRadio.RFPower,
                    FirstEvent = firstConnection,
                });

                firstConnection &= false;
            }

        }


        private void Radio_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            var radio = ((Radio)sender);
            switch (e.PropertyName)
            {
                case "RFPower":
                    System.Diagnostics.Debug.Print("radio.RFPower {0}", radio.RFPower);
                    OnRadioContextChanged();
                    break;

                default:
                    break;
            }
        }

        internal void SetRFPower(int power)
        {
            this.currentRadio.RFPower = power;
        }
    }
}
