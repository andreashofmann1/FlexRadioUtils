using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;

namespace FlexUtils
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class FlexUtils : Window
    {
        private readonly RadioAccess radioAccess;
        private readonly  State state;

        public FlexUtils()
        {
            InitializeComponent();

            this.radioAccess = RadioAccess.Instance;

            this.state = State.Instance;
            this.DataContext = this.state;

            switch (this.state.CurrentPowerLevel)
            {
                case PowerLevel.Low:
                    rbLow.IsChecked = true;
                    break;

                case PowerLevel.Mid:
                    rbMid.IsChecked = true;
                    break;

                case PowerLevel.High:
                    rbHigh.IsChecked = true;
                    break;
            }

            Events.RadioAdded += Events_RadioAdded;
            Events.RadioContextChanged += Events_RadioContextChanged;
            this.state.PropertyChanged += State_PropertyChanged;
        }

        private void State_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (this.Dispatcher.CheckAccess())
            {
                State_PropertyChangedInternal(sender, e);
            }
            else
            {
                this.Dispatcher.Invoke(new Action<object, PropertyChangedEventArgs>(State_PropertyChangedInternal), sender, e);
            }
        }

        private void State_PropertyChangedInternal(object sender, PropertyChangedEventArgs e)
        {
            State state = ((State)sender);
            switch (e.PropertyName)
            {
                case "CurrentTxPowerLow":
                    this.radioAccess.SetRFPower(state.CurrentTxPowerLow);
                    break;

                case "CurrentTxPowerMid":
                    this.radioAccess.SetRFPower(state.CurrentTxPowerMid);
                    break;

                case "CurrentTxPowerHigh":
                    this.radioAccess.SetRFPower(state.CurrentTxPowerHigh);
                    break;
            }

            switch (e.PropertyName)
            {
                case "CurrentTxPowerLow":
                case "CurrentTxPowerMid":
                case "CurrentTxPowerHigh":
                    this.tbLow.Text = string.Format("Low: {0}W", state.CurrentTxPowerLow);
                    this.tbMid.Text = string.Format("Mid: {0}W", state.CurrentTxPowerMid);
                    this.tbHigh.Text = string.Format("High: {0}W", state.CurrentTxPowerHigh);
                    break;
            }
        }

        private void Events_RadioContextChanged(Events.RadioContextChangedEventArgs e)
        {
            if (this.Dispatcher.CheckAccess())
            {
                this._events_RadioContextChanged(e);
            }
            else
            {
                this.Dispatcher.Invoke(new Action<Events.RadioContextChangedEventArgs>(_events_RadioContextChanged), e);
            }
        }

        private void _events_RadioContextChanged(Events.RadioContextChangedEventArgs e)
        {
            if (this.state.CurrentBand != e.CurrentBand || this.state.CurrentTx != e.CurrentTX)
            {
                this.state.CurrentBand = e.CurrentBand;
                this.state.CurrentTx = e.CurrentTX;
                this.tbRadioDetails.Text = string.Format("Band: {0}m - Antenna: {1}", e.CurrentBand, e.CurrentTX);
                this.state.LoadFromINI();
            }
            else
            {
                // we want to load the power from the INI, so the first time we ignore what we get from the radio
                if (!e.FirstEvent)
                {
                    this.state.SetCurrentPower(e.CurrentTXPower);
                    this.state.PersistStateToINI();
                }
            }
        }

        private void Events_RadioAdded(Events.RadioAddedEventArgs e)
        {
            if (this.Dispatcher.CheckAccess())
            {
                this._events_RadioAdded(e);
            }
            else
            {
                this.Dispatcher.Invoke(new Action<Events.RadioAddedEventArgs>(_events_RadioAdded), e);
            }
        }

        private void _events_RadioAdded(Events.RadioAddedEventArgs e)
        {
            this.tbRadioDescription.Text = e.Description;
        }

        private void OnChecked(object sender, RoutedEventArgs e)
        {
            var state = ((State)DataContext);

            switch (((RadioButton)sender).Name)
            {
                case "rbLow":
                    state.CurrentPowerLevel = PowerLevel.Low;
                    break;
                case "rbMid":
                    state.CurrentPowerLevel = PowerLevel.Mid;
                    break;
                case "rbHigh":
                    state.CurrentPowerLevel = PowerLevel.High;
                    break;
            }

            // TODO move to changed handler for Curent Power Level
            this.state.PersistGlobalSectionStateToINI();
            this.state.LoadFromINI();
        }
    }
}
