# Wifi connect

```
import network
import time

wlan = network.WLAN(network.STA_IF)
wlan.active(True)

ssid = "<SSID>"
pw = "<Password>"

wlan.connect(ssid, pw)

def light_onboard_led():
    led = machine.Pin('LED', machine.Pin.OUT)
    led.on();

timeout = 10
while timeout > 0:
    if wlan.status() >= 3:
        light_onboard_led()
        break
    timeout -= 1
    print('Waiting for connection...')
    time.sleep(1)
   
wlan_status = wlan.status()

print("My IP address is", wlan.ifconfig())
print(wlan_status)
```

# On-board - temp sense

```
import machine
import time

temp_sensor = machine.ADC(4)

while True:
    adc_voltage = temp_sensor.read_u16() * 3.3 / 65535
    cpu_temp = 27 - (adc_voltage - 0.706)/0.001721 # Formula given in RP2040 Datasheet
    print(cpu_temp)
    time.sleep_ms(1_000)
```

