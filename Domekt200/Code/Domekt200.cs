using System;
using DevBot9.Protocols.Homie;
using DevBot9.Protocols.Homie.Utilities;
using NLog;

namespace IotFleet.Shed;

partial class Domekt200 {
    private HostDevice _device;
    private readonly YahiTevuxHostConnection _broker = new();
    private readonly ReliableModbus _reliableModbus = new();

    public HostChoiceProperty ActualState { get; private set; }
    public HostChoiceProperty ActualVentilationLevelProperty { get; private set; }
    public HostNumberProperty SupplyAirTemperatureProperty { get; private set; }

    HostTextProperty _actualDateTimeProperty;
    HostChoiceProperty _targetState;
    HostChoiceProperty _actualModbusConnectionState;
    HostNumberProperty _disconnectCount;

    private readonly DateTime _startTime = DateTime.Now;
    private HostNumberProperty _systemUptime;

    public static Logger Log = LogManager.GetCurrentClassLogger();

    public Domekt200() { }
}
