using System;
using System.Device.I2c;
using UnitsNet;

namespace PoorMansCooler
{
    public class Sn3218
    {
        I2cDevice _device;

        public void Initialize()
        {
            var settings = new I2cConnectionSettings(1, 0x54);
            _device = I2cDevice.Create(settings);

#if !DEBUG

#endif
        }

        public void EnableDisableOutputs(int mask)
        {
            Span<byte> writeBuffer = stackalloc byte[2] {
                (byte)Register.EnableMasterOutput,
                0x01
            };
            _device.Write(writeBuffer);

            writeBuffer = stackalloc byte[4] {
                (byte)Register.EnableLeds,
                (byte)(mask & 0x3F),
                (byte)((mask >> 6) & 0x3F),
                (byte)((mask >> 12) & 0x3F)
            };
            _device.Write(writeBuffer);

            UpdateState();
        }

        public void Set(int channel, Ratio intensity)
        {
            byte valueToSend;

            if (intensity.DecimalFractions > 1) { valueToSend = 255; }
            if (intensity.DecimalFractions < 0) { valueToSend = 0; }

            valueToSend = (byte)(255 * intensity.DecimalFractions);

            Span<byte> writeBuffer = stackalloc byte[2] {
                (byte)((byte)Register.SetPwmValues + channel),
                valueToSend
            };
            _device.Write(writeBuffer);

            UpdateState();
        }

        public void Set(int channel)
        {
            Span<byte> writeBuffer = stackalloc byte[2] {
                (byte)((byte)Register.SetPwmValues + channel),
                255
            };
            _device.Write(writeBuffer);

            UpdateState();
        }

        public void Clear(int channel)
        {
            Span<byte> writeBuffer = stackalloc byte[2] {
                (byte)((byte)Register.SetPwmValues + channel),
                0
            };
            _device.Write(writeBuffer);

            UpdateState();
        }

        public void SetIntensities(int mask, byte intensity)
        {
            // var validatedIntensity = intensity;
            // if (intensity > 1.0) { validatedIntensity = 1.0; }
            //  if (intensity < 0.0) { validatedIntensity = 0.0; }
            Span<byte> writeBuffer = stackalloc byte[19];
            writeBuffer[0] = (byte)Register.SetPwmValues;
            for (int i = 1; i < 19; i++)
            {
                if (((mask >> (i - 1)) & 1) == 1)
                {
                    writeBuffer[i] = intensity;
                }
                else
                {
                    writeBuffer[i] = 0;
                }
            }
            _device.Write(writeBuffer);

            UpdateState();
        }

        private void UpdateState()
        {
            Span<byte> writeBuffer = stackalloc byte[2] {
                (byte)Register.Update,
                0xFF
            };
            _device.Write(writeBuffer);
        }

        private enum Register : byte
        {
            EnableMasterOutput = 0x00,
            SetPwmValues = 0x01,
            EnableLeds = 0x13,
            Update = 0x16,
            Reset = 0x17
        }
    }
}
