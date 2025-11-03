using System;
using TwinCAT.Ads;

namespace BeckhoffTwinCatAdsDemo
{
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
}