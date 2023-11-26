$ pip3 install atcom

$ atcom --port /dev/ttyUSB2 "AT+CPIN?"
AT+CPIN?
+CPIN: READY

OK




$ python3 dev/timelapse/scripts/PowerOnSIM7600.py 

$ atcom --port /dev/serial0 "AT+CPIN?"
AT+CPIN?
+CPIN: READY

OK


# Set text mode
```
$ atcom --port /dev/serial0 "AT+CMGF=1"

OK

```

# Check message storage
```
$ atcom --port /dev/serial0 "AT+CPMS?"

+CPMS: "SM",10,10,"SM",10,10,"SM",10,10

OK
```


# Read all messages
```
$ atcom --port /dev/serial0 'AT+CMGL="ALL"'
+CMGL: 0,"REC READ","791101017890","","23/09/13,14:38:05+48"
You have no Prepay credit. For easy ways to top up check out one.nz/topup or view your balance and top up from our My One NZ app.
+CMGL: 2,"REC READ","+64xxxxxxxxx","","23/09/14,17:01:14+48"
Hello
+CMGL: 3,"REC READ","+64xxxxxxxxx","","23/09/14,17:17:16+48"
Hello 2

OK
```

# Delete message at index 0
```
$ atcom --port /dev/serial0 'AT+CMGD=0'

OK
```

# Delete all messages
```
$ atcom --port /dev/serial0 'AT+CMGD=0,1'

OK
```

pi@timelapse-camera-ethan:~ $ lsusb
Bus 001 Device 003: ID 1e0e:9011 Qualcomm / Option SimTech, Incorporated
Bus 001 Device 001: ID 1d6b:0002 Linux Foundation 2.0 root hub








https://www.waveshare.com/wiki/Raspberry_Pi_RNDIS_dial-up_Internet_access


ls /dev/ttyUSB*
sudo apt-get install minicom
sudo minicom -D /dev/ttyUSB2

    Send the following command through minicom, then wait for the module to restart

AT+CUSBPIDSWITCH=9011,1,1

```
$ atcom --port /dev/serial0 'AT+CUSBPIDSWITCH=9011,1,1'

OK
```




$ ifconfig
lo: flags=73<UP,LOOPBACK,RUNNING>  mtu 65536
        inet 127.0.0.1  netmask 255.0.0.0
        inet6 ::1  prefixlen 128  scopeid 0x10<host>
        loop  txqueuelen 1000  (Local Loopback)
        RX packets 34  bytes 5388 (5.2 KiB)
        RX errors 0  dropped 0  overruns 0  frame 0
        TX packets 34  bytes 5388 (5.2 KiB)
        TX errors 0  dropped 0 overruns 0  carrier 0  collisions 0

tailscale0: flags=4305<UP,POINTOPOINT,RUNNING,NOARP,MULTICAST>  mtu 1280
        inet 100.99.137.23  netmask 255.255.255.255  destination 100.99.137.23
        inet6 fd7a:115c:a1e0:ab12:4843:cd96:6263:8917  prefixlen 128  scopeid 0x0<global>
        inet6 fe80::5ead:5c8d:818f:9505  prefixlen 64  scopeid 0x20<link>
        unspec 00-00-00-00-00-00-00-00-00-00-00-00-00-00-00-00  txqueuelen 500  (UNSPEC)
        RX packets 336  bytes 32152 (31.3 KiB)
        RX errors 0  dropped 0  overruns 0  frame 0
        TX packets 265  bytes 151544 (147.9 KiB)
        TX errors 0  dropped 0 overruns 0  carrier 0  collisions 0

wlan0: flags=4163<UP,BROADCAST,RUNNING,MULTICAST>  mtu 1500
        inet 172.20.10.189  netmask 255.255.255.0  broadcast 172.20.10.255
        inet6 fe80::3c49:882d:b422:dc54  prefixlen 64  scopeid 0x20<link>
        ether b8:27:eb:f2:b7:bf  txqueuelen 1000  (Ethernet)
        RX packets 1307  bytes 226152 (220.8 KiB)
        RX errors 0  dropped 0  overruns 0  frame 0
        TX packets 2143  bytes 1913408 (1.8 MiB)
        TX errors 0  dropped 0 overruns 0  carrier 0  collisions 0

wwan0: flags=4163<UP,BROADCAST,RUNNING,MULTICAST>  mtu 1500
        inet 169.254.224.15  netmask 255.255.0.0  broadcast 169.254.255.255
        inet6 fe80::745e:2070:1e49:c19b  prefixlen 64  scopeid 0x20<link>
        ether 9a:43:75:56:8f:7e  txqueuelen 1000  (Ethernet)
        RX packets 0  bytes 0 (0.0 B)
        RX errors 0  dropped 0  overruns 0  frame 0
        TX packets 32  bytes 5180 (5.0 KiB)
        TX errors 0  dropped 0 overruns 0  carrier 0  collisions 0






curl -s http://icanhazip.com

Verify IP address is different to wireless network.




sudo apt-get install libqmi-utils

./