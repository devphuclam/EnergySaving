# IDEA Utility Monitoring Platform

IUMP is an internal decision-support product for observing utility use and coordinating human
responses. It never controls equipment and does not make professional energy conclusions.

## Language

**Measurement Point**:
A stable, configured location on an asset where one metric is observed in one canonical unit.
_Avoid_: Tag, sensor, channel

**Measurement**:
An observed value with source time, receipt time, source, mapping version, quality, and identity.
_Avoid_: Reading, datapoint

**Data Source**:
An approved origin of measurements, such as Simulator, CSV, REST, or a conditional Edge source.
_Avoid_: Device, connector

**Source Mapping**:
An effective-dated, approved association from a source-specific key to a Measurement Point.
_Avoid_: Tag mapping, register mapping (unless specifically discussing Modbus)

**Data Quality**:
The Good, Uncertain, or Bad classification of a stored Measurement and its reason.
_Avoid_: Validity

**No Data**:
A derived source status indicating that an expected Measurement did not arrive within the grace
period. It is never a Measurement with value zero.
_Avoid_: Missing value, zero reading

**Rule Version**:
An immutable, tested and approved definition used to evaluate Measurements or No Data state.
_Avoid_: Rule configuration

**Evaluation Evidence**:
The immutable observations, time window, and Rule Version explaining a violation or recovery.
_Avoid_: Rule result

**Alert**:
A human-owned operational case created from Evaluation Evidence and progressed through its
controlled lifecycle. Recovery does not close an Alert.
_Avoid_: Alarm, notification

**Notification**:
A delivery record that informs a recipient about an event; it never owns or resolves an Alert.
_Avoid_: Alert

**Report**:
A versioned, time-bounded presentation of aggregates, quality coverage, Alerts, and limitations.
_Avoid_: Dashboard export

**Edge Collector**:
A conditional MVP-2 process that reads approved OT data, buffers it durably, and sends it outbound.
It has no command or write-back capability.
_Avoid_: Gateway controller, agent
