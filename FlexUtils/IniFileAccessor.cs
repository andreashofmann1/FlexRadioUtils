using System;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;

/// <summary>
/// Thread-safe class for access of ini files without using Win32 APIs one key at a time.
/// </summary>
/// <remarks></remarks>
public class IniFileAccessor
{
    private ConcurrentDictionary<string, ConcurrentDictionary<string, string>> ini = new ConcurrentDictionary<string, ConcurrentDictionary<string, string>>(StringComparer.InvariantCultureIgnoreCase);
    // ReaderWriterLock vs SyncLock is explained here: http://msdn.microsoft.com/en-us/magazine/cc163846.aspx
    // http://blogs.msdn.com/b/pedram/archive/2007/10/07/a-performance-comparison-of-readerwriterlockslim-with-readerwriterlock.aspx
    private readonly ReaderWriterLockSlim @lock = new ReaderWriterLockSlim();
    private string filePath = null;
    private bool isDirty = false;

    public IniFileAccessor(string iniFilePath)
    {
        filePath = iniFilePath;

        string txt = null;

        // This allows to open the file for read even some other process has it open for Read or Write! (http://stackoverflow.com/questions/1389155/easiest-way-to-read-text-file-which-is-locked-by-another-application)
        using (var fs = new FileStream(iniFilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
        {
            using (var textReader = new StreamReader(fs))
            {
                txt = textReader.ReadToEnd();
            }
        }

        string parm;

        ConcurrentDictionary<string, string> currentSection = new ConcurrentDictionary<string, string>(StringComparer.InvariantCultureIgnoreCase);
        ini[""] = currentSection;

        foreach (string line in txt.Split(new[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries).Where(t => !string.IsNullOrWhiteSpace(t)).Select(t => t.Trim()))
        {
            if (line.StartsWith(";"))
                continue;

            if (line.StartsWith("[") && line.EndsWith("]"))
            {
                currentSection = new ConcurrentDictionary<string, string>(StringComparer.InvariantCultureIgnoreCase);
                ini[line.Substring(1, line.LastIndexOf("]") - 1)] = currentSection;
                continue;
            }

            var idx = line.IndexOf("=");
            if (idx == -1)
                currentSection[line] = "";
            else
            {
                parm = (line.Substring(0, idx)).Trim();
                currentSection[parm] = line.Substring(idx + 1);
            }
        }
    }


    public string GetValue(string key)
    {
        return GetValue(key, "", "");
    }

    public string GetValue(string key, string section)
    {
        return GetValue(key, section, "");
    }

    public string GetValue(string key, string section, string @default)
    {
        @lock.TryEnterReadLock(Timeout.Infinite);
        try
        {
            if (!ini.ContainsKey(section))
                return @default;

            if (!ini[section].ContainsKey(key))
                return @default;

            var value = ini[section][key];
            if (!string.IsNullOrWhiteSpace(value))
                return value;
            else
                return @default;
        }
        finally
        {
            @lock.ExitReadLock();
        }
    }

    public string[] GetKeys(string section)
    {
        @lock.TryEnterReadLock(Timeout.Infinite);
        try
        {
            if (!ini.ContainsKey(section))
                return new string[0] { };

            return ini[section].Keys.ToArray();
        }
        finally
        {
            @lock.ExitReadLock();
        }
    }

    public string[] GetSections()
    {
        @lock.TryEnterReadLock(Timeout.Infinite);
        try
        {
            return ini.Keys.Where(t => t != "").ToArray();
        }
        finally
        {
            @lock.ExitReadLock();
        }
    }

    public void SaveValue(string key, string section, string value)
    {
        @lock.TryEnterWriteLock(Timeout.Infinite);
        try
        {
            var _isDirty = false;
            ConcurrentDictionary<string, string> currentSection = null;
            if (ini.ContainsKey(section))
                currentSection = ini[section];
            else
            {
                _isDirty = true;
                currentSection = new ConcurrentDictionary<string, string>(StringComparer.InvariantCultureIgnoreCase);
                ini[section] = currentSection;
            }

            if (!currentSection.ContainsKey(key))
            {
                _isDirty = true;
                currentSection[key] = value;
            }
            else
            {
                var oldvalue = currentSection[key];
                if (string.Compare(oldvalue, value) == 0)
                {
                }
                else
                {
                    _isDirty = true;
                    currentSection[key] = value;
                }
            }

            if (_isDirty)
                this.isDirty = true;

            if (_isDirty || this.isDirty)
            {
                SaveFile(this.filePath, this.ini);
                // if we failed to write to the file in SaveFile, we will keep Me.isDirty set to true, therefore will save next time, even if nothing else has changed.
                this.isDirty = false;
            }
        }
        finally
        {
            @lock.ExitWriteLock();
        }
    }

    public static void EnsureFileExists(string iniFilePath)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(iniFilePath));
        if (!File.Exists(iniFilePath))
            WriteAllTextToFileCreateOrOverwrite(iniFilePath, string.Empty);
    }

    public void RemovePrivateProfileSection(string SectionName, string filename)
    {
        @lock.TryEnterWriteLock(Timeout.Infinite);
        try
        {
            ConcurrentDictionary<string, string> val = new ConcurrentDictionary<string, string>();
            ini.TryRemove(SectionName, out val);
        }
        finally
        {
            @lock.ExitWriteLock();
        }
    }

    private static void SaveFile(string filePath, ConcurrentDictionary<string, ConcurrentDictionary<string, string>> ini)
    {
        // clean up of empty sections
        for (var i = ini.Keys.Count - 1; i >= 0; i += -1)
        {
            var section = ini[ini.Keys.ElementAt(i)];
            if (section.Count == 0)
            {
                ConcurrentDictionary<string, string> tempDict = null;
                ini.TryRemove(ini.Keys.ElementAt(i), out tempDict);
            }
        }

        var sb = new StringBuilder();
        var sortedSectionsQuery = from section in ini
                                  orderby section.Key ascending
                                  select section;

        foreach (var sectionKeyPair in sortedSectionsQuery)
        {
            var sectionKey = sectionKeyPair.Key;
            var section = sectionKeyPair.Value;
            sb.AppendLine();
            sb.AppendFormat("[{0}]{1}", sectionKey, Environment.NewLine);

            var sortedSettingsQuery = from setting in section
                                      orderby setting.Key ascending
                                      select setting;

            foreach (var settingsKeyPair in sortedSettingsQuery)
            {
                var settingsKey = settingsKeyPair.Key;
                var settingsValue = settingsKeyPair.Value;
                if (!string.IsNullOrWhiteSpace(settingsValue))
                    sb.AppendFormat("{0}={1}{2}", settingsKey, settingsValue, Environment.NewLine);
            }
        }

        WriteAllTextToFileCreateOrOverwrite(filePath, sb.ToString());
    }

    private static void WriteAllTextToFileCreateOrOverwrite(string filePath, string textToWrite)
    {
        using (var fs = new FileStream(filePath, FileMode.Create, FileAccess.ReadWrite, FileShare.ReadWrite))
        {
            if (!string.IsNullOrWhiteSpace(textToWrite))
            {
                using (var writer = new StreamWriter(fs))
                {
                    writer.Write(textToWrite);
                }
            }
        }
    }
}
