using Newtonsoft.Json;
using Spectre.Console;
using System;
using System.IO;
using System.Text;

namespace BeckhoffTwinCatAdsDemo
{
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