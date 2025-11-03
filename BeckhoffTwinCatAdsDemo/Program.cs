using Newtonsoft.Json;
using Spectre.Console;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using TwinCAT.Ads;

namespace BeckhoffTwinCatAdsDemo
{
    public class Settings
    {
        public string NetId { get; set; }
        public int Port { get; set; }
        public string IntValueAddress { get; set; }
        public string DoubleValueAddress { get; set; }
        public string StringValueAddress { get; set; }
        public string BoolValueAddress { get; set; }
    }

    public class PlcService : IDisposable
    {
        private readonly AdsClient _client;
        private readonly Settings _settings;

        private readonly uint _intValueHandle;
        private readonly uint _doubleValueHandle;
        private readonly uint _stringValueHandle;
        private readonly uint _boolValueHandle;

        public PlcService(Settings settings)
        {
            _settings = settings;
            _client = new AdsClient();
            try
            {
                _client.Connect(_settings.NetId, _settings.Port);
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to connect to PLC with NetId '{_settings.NetId}' and Port '{_settings.Port}'.", ex);
            }

            if (!_client.IsConnected)
            {
                throw new Exception("Could not connect to PLC.");
            }

            try
            {
                _intValueHandle = _client.CreateVariableHandle(_settings.IntValueAddress);
                _doubleValueHandle = _client.CreateVariableHandle(_settings.DoubleValueAddress);
                _stringValueHandle = _client.CreateVariableHandle(_settings.StringValueAddress);
                _boolValueHandle = _client.CreateVariableHandle(_settings.BoolValueAddress);
            }
            catch (Exception ex)
            {
                throw new Exception("Failed to create variable handles. Make sure the variables are defined in the PLC project and the addresses are correct in the settings.", ex);
            }
        }

        public int ReadInt()
        {
            try
            {
                return (int)_client.ReadAny(_intValueHandle, typeof(int));
            }
            catch (Exception ex)
            {
                throw new Exception("Failed to read integer value.", ex);
            }
        }

        public double ReadDouble()
        {
            try
            {
                return (double)_client.ReadAny(_doubleValueHandle, typeof(double));
            }
            catch (Exception ex)
            {
                throw new Exception("Failed to read double value.", ex);
            }
        }

        public string ReadString()
        {
            try
            {
                return (string)_client.ReadAny(_stringValueHandle, typeof(string), new int[] { 81 });
            }
            catch (Exception ex)
            {
                throw new Exception("Failed to read string value.", ex);
            }
        }

        public bool ReadBool()
        {
            try
            {
                return (bool)_client.ReadAny(_boolValueHandle, typeof(bool));
            }
            catch (Exception ex)
            {
                throw new Exception("Failed to read bool value.", ex);
            }
        }

        public void WriteInt(int value)
        {
            try
            {
                _client.WriteAny(_intValueHandle, value);
            }
            catch (Exception ex)
            {
                throw new Exception("Failed to write integer value.", ex);
            }
        }

        public void WriteDouble(double value)
        {
            try
            {
                _client.WriteAny(_doubleValueHandle, value);
            }
            catch (Exception ex)
            {
                throw new Exception("Failed to write double value.", ex);
            }
        }

        public void WriteString(string value)
        {
            try
            {
                _client.WriteAny(_stringValueHandle, value, new int[] { 81 });
            }
            catch (Exception ex)
            {
                throw new Exception("Failed to write string value.", ex);
            }
        }

        public void WriteBool(bool value)
        {
            try
            {
                _client.WriteAny(_boolValueHandle, value);
            }
            catch (Exception ex)
            {
                throw new Exception("Failed to write bool value.", ex);
            }
        }

        public void Dispose()
        {
            _client.DeleteVariableHandle(_intValueHandle);
            _client.DeleteVariableHandle(_doubleValueHandle);
            _client.DeleteVariableHandle(_stringValueHandle);
            _client.DeleteVariableHandle(_boolValueHandle);
            _client.Dispose();
        }
    }

    public class UserInterface
    {
        private readonly Settings _settings;
        private readonly Action<Settings> _saveSettings;

        public UserInterface(Settings settings, Action<Settings> saveSettings)
        {
            _settings = settings;
            _saveSettings = saveSettings;
        }

        public void ShowMenu()
        {
            while (true)
            {
                AnsiConsole.Clear();
                AnsiConsole.WriteLine("1. Read Values");
                AnsiConsole.WriteLine("2. Write Values");
                AnsiConsole.WriteLine("3. View Configuration");
                AnsiConsole.WriteLine("4. Configure");
                AnsiConsole.WriteLine("5. Exit");

                var choice = AnsiConsole.Ask<int>("Select an option:");

                switch (choice)
                {
                    case 1:
                        ReadValues();
                        break;
                    case 2:
                        WriteValues();
                        break;
                    case 3:
                        ViewConfiguration();
                        break;
                    case 4:
                        Configure();
                        break;
                    case 5:
                        return;
                }
                AnsiConsole.WriteLine("Press any key to continue...");
                Console.ReadKey();
            }
        }

        private void ReadValues()
        {
            var results = new List<Tuple<string, string>>();
            try
            {
                using (var plcService = new PlcService(_settings))
                {
                    try
                    {
                        results.Add(new Tuple<string, string>("Integer", plcService.ReadInt().ToString()));
                    }
                    catch (Exception ex)
                    {
                        results.Add(new Tuple<string, string>("Integer", ex.Message));
                    }

                    try
                    {
                        results.Add(new Tuple<string, string>("Double", plcService.ReadDouble().ToString()));
                    }
                    catch (Exception ex)
                    {
                        results.Add(new Tuple<string, string>("Double", ex.Message));
                    }

                    try
                    {
                        results.Add(new Tuple<string, string>("String", plcService.ReadString()));
                    }
                    catch (Exception ex)
                    {
                        results.Add(new Tuple<string, string>("String", ex.Message));
                    }

                    try
                    {
                        results.Add(new Tuple<string, string>("Bool", plcService.ReadBool().ToString()));
                    }
                    catch (Exception ex)
                    {
                        results.Add(new Tuple<string, string>("Bool", ex.Message));
                    }
                }
            }
            catch (Exception ex)
            {
                PrintFilteredException(ex);
                return;
            }

            var table = new Table();
            table.AddColumn("Variable");
            table.AddColumn("Value");

            foreach (var result in results)
            {
                table.AddRow(result.Item1, result.Item2);
            }

            AnsiConsole.Write(table);
        }

        private void WriteValues()
        {
            var results = new List<Tuple<string, string>>();
            try
            {
                using (var plcService = new PlcService(_settings))
                {
                    try
                    {
                        var intValue = AnsiConsole.Ask<int>("Enter integer value:");
                        plcService.WriteInt(intValue);
                        results.Add(new Tuple<string, string>("Integer", "Success"));
                    }
                    catch (Exception ex)
                    {
                        results.Add(new Tuple<string, string>("Integer", ex.Message));
                    }

                    try
                    {
                        var doubleValue = AnsiConsole.Ask<double>("Enter double value:");
                        plcService.WriteDouble(doubleValue);
                        results.Add(new Tuple<string, string>("Double", "Success"));
                    }
                    catch (Exception ex)
                    {
                        results.Add(new Tuple<string, string>("Double", ex.Message));
                    }

                    try
                    {
                        var stringValue = AnsiConsole.Ask<string>("Enter string value:");
                        plcService.WriteString(stringValue);
                        results.Add(new Tuple<string, string>("String", "Success"));
                    }
                    catch (Exception ex)
                    {
                        results.Add(new Tuple<string, string>("String", ex.Message));
                    }

                    try
                    {
                        var boolValue = AnsiConsole.Confirm("Enter bool value:");
                        plcService.WriteBool(boolValue);
                        results.Add(new Tuple<string, string>("Bool", "Success"));
                    }
                    catch (Exception ex)
                    {
                        results.Add(new Tuple<string, string>("Bool", ex.Message));
                    }
                }
            }
            catch (Exception ex)
            {
                PrintFilteredException(ex);
                return;
            }

            var table = new Table();
            table.AddColumn("Variable");
            table.AddColumn("Status");

            foreach (var result in results)
            {
                table.AddRow(result.Item1, result.Item2);
            }

            AnsiConsole.Write(table);
        }

        private void PrintFilteredException(Exception ex)
        {
            var exceptionText = ex.ToString();
            var solutionPath = AppDomain.CurrentDomain.BaseDirectory.Replace("\\bin\\Debug\\", "");
            exceptionText = exceptionText.Replace(solutionPath, string.Empty);
            AnsiConsole.WriteLine(exceptionText);
        }

        private void ViewConfiguration()
        {
            var table = new Table();
            table.AddColumn("Setting");
            table.AddColumn("Value");

            table.AddRow("NetId", _settings.NetId);
            table.AddRow("Port", _settings.Port.ToString());
            table.AddRow("Integer Value Address", _settings.IntValueAddress);
            table.AddRow("Double Value Address", _settings.DoubleValueAddress);
            table.AddRow("String Value Address", _settings.StringValueAddress);
            table.AddRow("Bool Value Address", _settings.BoolValueAddress);

            AnsiConsole.Write(table);
        }

        private void Configure()
        {
            _settings.NetId = AnsiConsole.Ask<string>("Enter NetId:", string.IsNullOrWhiteSpace(_settings.NetId) ? "127.0.0.1.1.1" : _settings.NetId);
            _settings.Port = AnsiConsole.Ask<int>("Enter Port:", _settings.Port == 0 ? 851 : _settings.Port);
            _settings.IntValueAddress = AnsiConsole.Ask<string>("Enter Integer Value Address:", string.IsNullOrWhiteSpace(_settings.IntValueAddress) ? "MAIN.nInteger" : _settings.IntValueAddress);
            _settings.DoubleValueAddress = AnsiConsole.Ask<string>("Enter Double Value Address:", string.IsNullOrWhiteSpace(_settings.DoubleValueAddress) ? "MAIN.fReal" : _settings.DoubleValueAddress);
            _settings.StringValueAddress = AnsiConsole.Ask<string>("Enter String Value Address:", string.IsNullOrWhiteSpace(_settings.StringValueAddress) ? "MAIN.sString" : _settings.StringValueAddress);
            _settings.BoolValueAddress = AnsiConsole.Ask<string>("Enter Bool Value Address:", string.IsNullOrWhiteSpace(_settings.BoolValueAddress) ? "MAIN.bBool" : _settings.BoolValueAddress);

            _saveSettings(_settings);
            AnsiConsole.WriteLine("Configuration saved.");
        }
    }

    class Program
    {
        private static readonly string SettingsFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "settings.json");

        static void Main(string[] args)
        {
            var settings = LoadSettings();
            var ui = new UserInterface(settings, SaveSettings);
            ui.ShowMenu();
        }

        private static Settings LoadSettings()
        {
            if (File.Exists(SettingsFilePath))
            {
                var json = File.ReadAllText(SettingsFilePath);
                return JsonConvert.DeserializeObject<Settings>(json);
            }
            else
            {
                var settings = new Settings
                {
                    NetId = AnsiConsole.Ask<string>("Enter NetId:", "127.0.0.1.1.1"),
                    Port = AnsiConsole.Ask<int>("Enter Port:", 851),
                    IntValueAddress = AnsiConsole.Ask<string>("Enter Integer Value Address:", "MAIN.nInteger"),
                    DoubleValueAddress = AnsiConsole.Ask<string>("Enter Double Value Address:", "MAIN.fReal"),
                    StringValueAddress = AnsiConsole.Ask<string>("Enter String Value Address:", "MAIN.sString"),
                    BoolValueAddress = AnsiConsole.Ask<string>("Enter Bool Value Address:", "MAIN.bBool")
                };

                if (string.IsNullOrWhiteSpace(settings.NetId))
                {
                    settings.NetId = "127.0.0.1.1.1";
                }
                if (string.IsNullOrWhiteSpace(settings.IntValueAddress))
                {
                    settings.IntValueAddress = "MAIN.nInteger";
                }
                if (string.IsNullOrWhiteSpace(settings.DoubleValueAddress))
                {
                    settings.DoubleValueAddress = "MAIN.fReal";
                }
                if (string.IsNullOrWhiteSpace(settings.StringValueAddress))
                {
                    settings.StringValueAddress = "MAIN.sString";
                }
                if (string.IsNullOrWhiteSpace(settings.BoolValueAddress))
                {
                    settings.BoolValueAddress = "MAIN.bBool";
                }

                SaveSettings(settings);
                return settings;
            }
        }

        private static void SaveSettings(Settings settings)
        {
            var json = JsonConvert.SerializeObject(settings, Formatting.Indented);
            File.WriteAllText(SettingsFilePath, json);
        }
    }
}