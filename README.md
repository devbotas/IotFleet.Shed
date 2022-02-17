Control hub for my engineering shed (all the heaters, recuperators, solar inverters sit there). 

It is a standalone balena.io device, running on Raspbery Pi. It host its own instances of InfluxDB and Mosquitto MQTT broker, and so is completely independant from central home infrastructure. However, it does synchronize its MQTT Homie topics with the central MQTT server, so the central system knows about the Shed, but other than that, Shed is completely independent.
