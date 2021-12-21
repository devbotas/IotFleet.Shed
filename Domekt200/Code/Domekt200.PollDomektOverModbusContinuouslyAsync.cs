using System;
using System.Threading;
using System.Threading.Tasks;

namespace IotFleet.Shed;

partial class Domekt200 {
    private async Task PollDomektOverModbusContinuouslyAsync(CancellationToken cancellationToken) {
        Log.Info($"Spinning up parameter monitoring task.");
        while (true) {
            if (_reliableModbus.IsConnected == false) { continue; }

            try {
                var allOk = _reliableModbus.TryReadModbusRegister(KomfoventRegisters.StartStop, out var startStopStatus);
                if (allOk) {
                    //
                }

                allOk = TryReadDateTime(out var dateTime);
                if (allOk) {
                    _actualDateTimeProperty.Value = dateTime.ToString("yyyy-MM-ddTHH:mm:ss");
                }
                else {
                    Log.Warn("Failed to parse read date.");
                }

                allOk = _reliableModbus.TryReadModbusRegister(KomfoventRegisters.VentilationLevelManual, out var ventilationLevelManual);
                if (allOk) {
                    //
                }

                allOk = _reliableModbus.TryReadModbusRegister(KomfoventRegisters.VentilationLevelCurrent, out var ventilationLevelCurrent);
                if (allOk) {
                    switch (ventilationLevelCurrent) {
                        case 0:
                            ActualVentilationLevelProperty.Value = "OFF";
                            break;

                        case 1:
                            ActualVentilationLevelProperty.Value = "LOW";
                            break;

                        case 2:
                            ActualVentilationLevelProperty.Value = "MEDIUM";
                            break;

                        case 3:
                            ActualVentilationLevelProperty.Value = "HIGH";
                            break;
                    }
                }

                allOk = _reliableModbus.TryReadModbusRegister(KomfoventRegisters.ModeAutoManual, out var ventilationMode);
                if (allOk) {
                    if (ventilationMode == 0) { ActualVentilationLevelProperty.Value = "MANUAL"; }
                    if (ventilationMode == 1) { ActualVentilationLevelProperty.Value = "AUTOMATIC"; }
                }

                if (allOk) {
                    if (startStopStatus == 0) { ActualState.Value = "OFF"; }
                    else if ((startStopStatus == 1) && (ventilationMode == 0)) {
                        switch (ventilationLevelCurrent) {
                            case 1:
                                ActualState.Value = "ON-LOW";
                                break;

                            case 2:
                                ActualState.Value = "ON-MEDIUM";
                                break;

                            case 3:
                                ActualState.Value = "ON-HIGH";
                                break;
                        }
                    }
                    else if ((startStopStatus == 1) && (ventilationMode == 1)) {
                        ActualState.Value = "ON-AUTO";
                    }
                    else {
                        ActualState.Value = "UNKNOWN";
                    }
                }

                allOk = _reliableModbus.TryReadModbusRegister(KomfoventRegisters.SupplyAirTemperature, out var supplyAirTemperature);
                if (allOk) {
                    if (Math.Abs(supplyAirTemperature / 10.0f) < 50) {
                        SupplyAirTemperatureProperty.Value = supplyAirTemperature / 10.0f;
                    }
                    else {
                        Log.Error($"Temperature reading was off-limits: {supplyAirTemperature / 10.0f:F1}. Skipping it.");
                    }
                }
            }
            catch (Exception ex) {
                Log.Error($"Reading registers failed, because: {ex.Message}");
            }

            await Task.Delay(1000, cancellationToken);
        }
    }
}
