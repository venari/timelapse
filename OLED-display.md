# OLED display

OLED12864 - Digole 128x64 OLED display
I2C address 0x27.

```
sudo apt-get install i2c-tools
```

```
sudo raspi-config
```

3 Interface Options -> I5 I2C -> Yes

```
$ sudo i2cdetect -l
i2c-1	i2c       	bcm2835 (i2c@7e804000)          	I2C adapter
i2c-2	i2c       	bcm2835 (i2c@7e805000)          	I2C adapter
```