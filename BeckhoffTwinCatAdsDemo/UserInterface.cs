using Spectre.Console;
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace BeckhoffTwinCatAdsDemo
{
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
            var solutionName = typeof(Program).Namespace;
            var regex = new Regex($@".*?(?={solutionName})");
            var lines = exceptionText.Split(new[] { Environment.NewLine }, StringSplitOptions.None);
            for (int i = 0; i < lines.Length; i++)
            {
                var line = lines[i];
                var index = line.IndexOf(" in ");
                if (index > 0)
                {
                    var prefix = line.Substring(0, index + 4);
                    var pathPart = line.Substring(index + 4);
                    pathPart = regex.Replace(pathPart, string.Empty, 1);
                    lines[i] = prefix + pathPart;
                }
            }
            exceptionText = string.Join(Environment.NewLine, lines);
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
}
