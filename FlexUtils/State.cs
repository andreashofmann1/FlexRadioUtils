using Flex.UiWpfFramework.Mvvm;
using System;
using System.Collections.Concurrent;
using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices;

namespace FlexUtils
{
    public sealed class State : ObservableObject
    {
        private PowerLevel _currentPowerLevel = PowerLevel.Low;
        private int _currentTxPowerLow = 0;
        private int _currentTxPowerMid = 0;
        private int _currentTxPowerHigh = 0;
        private string _currentTx = string.Empty;
        private string _currentBand = string.Empty;

        private static readonly Lazy<State> lazy = new Lazy<State>(() => new State());

        public static State Instance { get { return lazy.Value; } }

        private State()
        {
            IniFileAccessor.EnsureFileExists(iniFileName);

            var x = GetPrivateProfileString("Global", "CurrentPowerLevel", "", iniFileName);
            if (!string.IsNullOrWhiteSpace(x))
            {
                this.CurrentPowerLevel = (PowerLevel)Enum.Parse(typeof(PowerLevel), x);
            }
        }

        public PowerLevel CurrentPowerLevel
        {
            get
            {
                return _currentPowerLevel;
            }
            set
            {
                this._currentPowerLevel = value;
                RaisePropertyChanged("CurrentPowerLevel");
            }
        }
        public int CurrentTxPowerLow
        {
            get
            {
                return _currentTxPowerLow;
            }
            set
            {
                this._currentTxPowerLow = value;
                RaisePropertyChanged("CurrentTxPowerLow");
            }
        }

        public int CurrentTxPowerMid
        {
            get
            {
                return _currentTxPowerMid;
            }
            set
            {
                this._currentTxPowerMid = value;
                RaisePropertyChanged("CurrentTxPowerMid");
            }
        }

        public int CurrentTxPowerHigh
        {
            get
            {
                return _currentTxPowerHigh;
            }
            set
            {
                this._currentTxPowerHigh = value;
                RaisePropertyChanged("CurrentTxPowerHigh");
            }
        }


        public string CurrentTx
        {
            get
            {
                return _currentTx;
            }
            set
            {
                this._currentTx = value;
                RaisePropertyChanged("CurrentTx");
            }
        }

        internal void SetCurrentPower(int currentTXPower)
        {
            switch(this.CurrentPowerLevel)
            {
                case PowerLevel.Low:
                    this.CurrentTxPowerLow = currentTXPower;
                    break;

                case PowerLevel.Mid:
                    this.CurrentTxPowerMid = currentTXPower;
                    break;

                case PowerLevel.High:
                    this.CurrentTxPowerHigh = currentTXPower;
                    break;
            }
        }

        public string CurrentBand
        {
            get
            {
                return _currentBand;
            }
            set
            {
                this._currentBand = value;
                RaisePropertyChanged("CurrentBand");
            }
        }

        public void PersistGlobalSectionStateToINI()
        {
            this.WritePrivateProfileString("Global", "CurrentPowerLevel", this.CurrentPowerLevel.ToString(), iniFileName);
        }

        public void PersistStateToINI()
        {
            this.WritePrivateProfileString(string.Format("{0}-{1}", this.CurrentBand, this.CurrentTx), "CurrentTxPowerLow", this.CurrentTxPowerLow.ToString(), iniFileName);
            this.WritePrivateProfileString(string.Format("{0}-{1}", this.CurrentBand, this.CurrentTx), "CurrentTxPowerMid", this.CurrentTxPowerMid.ToString(), iniFileName);
            this.WritePrivateProfileString(string.Format("{0}-{1}", this.CurrentBand, this.CurrentTx), "CurrentTxPowerHigh", this.CurrentTxPowerHigh.ToString(), iniFileName);
        }

        public void LoadFromINI()
        {
            var x = GetPrivateProfileString("Global", "CurrentPowerLevel", "", iniFileName);
            if (!string.IsNullOrWhiteSpace(x))
            {
                this.CurrentPowerLevel = (PowerLevel)Enum.Parse(typeof(PowerLevel), x);
            }
            else
            {
                this._currentPowerLevel = PowerLevel.Low;
            }

            x = GetPrivateProfileString(string.Format("{0}-{1}", this.CurrentBand, this.CurrentTx), "CurrentTxPowerLow", "", iniFileName);
            if (!string.IsNullOrWhiteSpace(x))
            {
                this._currentTxPowerLow = int.Parse(x);
            }

            x = GetPrivateProfileString(string.Format("{0}-{1}", this.CurrentBand, this.CurrentTx), "CurrentTxPowerMid", "", iniFileName);
            if (!string.IsNullOrWhiteSpace(x))
            {
                this._currentTxPowerMid = int.Parse(x);
            }

            x = GetPrivateProfileString(string.Format("{0}-{1}", this.CurrentBand, this.CurrentTx), "CurrentTxPowerHigh", "", iniFileName);
            if (!string.IsNullOrWhiteSpace(x))
            {
                this._currentTxPowerHigh = int.Parse(x);
            }

            ForceTXPowerLevelUpdate();
        }

        private void ForceTXPowerLevelUpdate()
        {
            switch (this.CurrentPowerLevel)
            {
                case PowerLevel.Low:
                    RaisePropertyChanged("CurrentTxPowerLow");
                    break;

                case PowerLevel.Mid:
                    RaisePropertyChanged("CurrentTxPowerMid");
                    break;

                case PowerLevel.High:
                    RaisePropertyChanged("CurrentTxPowerHigh");
                    break;
            }
    }

    #region Persistance 
    private string iniFileName = Environment.ExpandEnvironmentVariables(@"%USERPROFILE%\AppData\Roaming\KU7T\FlexUtils\FlexUtils.ini");
        private ConcurrentDictionary<string, IniFileAccessor> iniFileAccessors = new ConcurrentDictionary<string, IniFileAccessor>();
        private object iniFileLockObject = new object();

        private IniFileAccessor GetIniFileAccessor(string fileName)
        {
            lock (iniFileLockObject)
            {
                var normalizedFileName = fileName.ToLowerInvariant();
                if (iniFileAccessors.ContainsKey(normalizedFileName))
                    return iniFileAccessors[normalizedFileName];
                else
                {
                    bool ret;

                    var accessor = new IniFileAccessor(normalizedFileName);
                    ret = iniFileAccessors.TryAdd(normalizedFileName, accessor);
                    if (ret == false)
                    {
                        // TODO LogError("GetIniFileAccessor TryAdd failed = " + normalizedFileName);
                        // TODO HandleError(new SystemException());
                    }
                    return accessor;
                }
            }
        }

        private string GetPrivateProfileString(string sectionName, string keyName, string defaultValue, string fileName)
        {
            string outValue = string.Empty;
            // Note: we retry up to 3 times. If we cannot read from the ini file in memory after 3 tries, we have a serious problem
            if (false == InvokeFunctionWithRetryForFileInUseException(new Func<string>(() =>
            {
                return GetIniFileAccessor(fileName).GetValue(keyName, sectionName, defaultValue);
            }), ref outValue, 3, 10))
            {
                // TODO HandleError(new Exception(string.Format("Was not able to read from ini file {0} in 3 tries.", fileName)));
                return defaultValue;
            }
            return outValue;
        }

        private bool WritePrivateProfileString(string sectionName, string keyName, string value, string fileName)
        {
            // Note: we retry up to 3 times. If we cannot write to the ini file we can ignore this, hoping it will save at other times
            var success = InvokeActionWithRetryForFileInUseException(new Action(() =>
            {
                GetIniFileAccessor(fileName).SaveValue(keyName, sectionName, value);
            }), 3, 10);
            // todo: ku7t: trace success
            return success;
        }

        public static bool InvokeFunctionWithRetryForFileInUseException(Func<string> action, ref string functionReturnValue, uint retryCount, int sleepBetweenRetriesInMS)
        {
            var done = false;
            uint numberOfReTry = 0;
            var retryAbleError = true;
            while (done == false && retryAbleError == true && retryCount >= numberOfReTry)
            {
                try
                {
                    functionReturnValue = action.Invoke();
                    done = true;
                    retryAbleError = false;
                }
                catch (IOException ex)
                {
                    var hresult = Marshal.GetHRForException(ex).ToString("X");
                    if (hresult == "80070020")
                    {
                        // 80070020 is HRESULT for file in use
                        retryAbleError = true;
                        // TODO Tracer.Warning("InvokeFunctionWithRetryForFileInUseException: caught IOException with HRESULT 80070020");
                    }
                    else if (hresult == "800704C8")
                    {
                        // 800704C8 is HRESULT for "The requested operation cannot be performed on a file with a user-mapped section open"
                        retryAbleError = true;
                        // TODO Tracer.Warning("InvokeFunctionWithRetryForFileInUseException: caught IOException with HRESULT 800704C8");
                    }
                    else
                        // do not need to trace an error, as exception bubbles up and will be eventually be caught and traced
                        throw;
                }
                catch (Exception)
                {
                    // do not need to trace an error, as exception bubbles up and will be eventually be caught and traced
                    throw;
                }

                if (retryAbleError == true)
                {
                    // TODO Tracer.Warning("InvokeFunctionWithRetryForFileInUseException: sleeping {0} ms before retrying if number of retries is not exhausted.", sleepBetweenRetriesInMS);
                    System.Threading.Thread.Sleep(sleepBetweenRetriesInMS);
                    numberOfReTry = System.Convert.ToUInt32(numberOfReTry + 1);
                }
            }

            return done;
        }

        public static bool InvokeActionWithRetryForFileInUseException(Action action, uint retryCount, int sleepBetweenRetriesInMS)
        {
            var done = false;
            uint numberOfReTry = 0;
            var retryAbleError = true;
            while (done == false && retryAbleError == true && retryCount >= numberOfReTry)
            {
                try
                {
                    action.Invoke();
                    done = true;
                    retryAbleError = false;
                }
                catch (IOException ex)
                {
                    var hresult = Marshal.GetHRForException(ex).ToString("X");
                    if (hresult == "80070020")
                    {
                        // 80070020 is HRESULT for file in use
                        retryAbleError = true;
                        // TODO Tracer.Warning("InvokeActionWithRetryForFileInUseException: caught IOException with HRESULT 80070020");
                    }
                    else if (hresult == "800704C8")
                    {
                        // 800704C8 is HRESULT for "The requested operation cannot be performed on a file with a user-mapped section open"
                        retryAbleError = true;
                        // TODO Tracer.Warning("InvokeActionWithRetryForFileInUseException: caught IOException with HRESULT 800704C8");
                    }
                    else
                        // do not need to trace an error, as exception bubbles up and will be eventually be caught and traced
                        throw;
                }
                catch (Exception)
                {
                    // do not need to trace an error, as exception bubbles up and will be eventually be caught and traced
                    throw;
                }

                if (retryAbleError == true)
                {
                    // TODO Tracer.Warning("InvokeActionWithRetryForFileInUseException: sleeping {0} ms before retrying if number of retries is not exhausted.", sleepBetweenRetriesInMS);
                    System.Threading.Thread.Sleep(sleepBetweenRetriesInMS);
                    numberOfReTry = System.Convert.ToUInt32(numberOfReTry + 1);
                }
            }

            return done;
        }

        #endregion
    }
}
