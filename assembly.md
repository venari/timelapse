# Assembly

## Solar Power
Parts:
- [ZM9056 - 12V 40W](https://www.jaycar.co.nz/12v-40w-monocrystalline-solar-panel/p/ZM9056)
- [PS1017 - Waterproof IP67 XLR Line Socket](https://www.jaycar.co.nz/waterproof-ip67-xlr-line-socket/p/PS1017)

1. Cut solar power connectors off solar panel, and solder XLR socker to wires. Positive to Pin <1 - check> and Negative to Pin <check - 2 or 3>
1. Wrap with heat shrink tubing.

## Power in

Parts:
- [PP1014 - IPX6 Surface Mount XLR Male Connector](https://www.jaycar.co.nz/ipx6-surface-mount-xlr-male-connector/p/PP1014)
- LM2596 based [Arduino Compatible DC Voltage Regulator](https://www.jaycar.co.nz/arduino-compatible-dc-voltage-regulator/p/XC4514)
- Terminal block - 2 terminals

1. Connect positive wire between Pin 1 of XLR Male Connector and terminal 1 of terminal block.
1. Connect negative wire between Pin <check - 2 or 3> and terminal 2 of terminal block.
1. Connect another positive wire between terminal 1 of terminal block and Positive IN of LM2596.
1. Connect another negative wire between terminal 2 of terminal block and Negative IN of LM2596.
1. Connect positive wire terminated in Jumper socket to Positive OUT of LM2596. This goes to [Positive Pin of J4](https://github.com/PiSupply/PiJuice/tree/master/Hardware#connectors) on PiJuice Zero.
1. Connect negative wire terminated in Jumper socket to Negative OUT of LM2596. This goes to Negative Pin of J4 on PiJuice Zero.

## Pi mounting

4 x M2.5 x 6 screw
1 x Plastic mount
4 x M2.5 x 6 Pozi Pan (DIN7985)
4 x M2.5 nut (DIN934)
1 x Pi Zero
4 x Pi Juice Zero spacer
1 x Pi Juice Zero
4 x Pi Juice Zero M2.5 screw

## Camera mounting and connecting

4 x M2 x 6 screw 
1 x Camera
1 x Plastic mount

## Mount camera mount to enclosure
<Outside>
4 x M2 x 6 screw 
<enclosure>
1 x Plastic mount
<Inside>

## Mount window to enclosure
Drill out 4 holes in acrylic to 2.5mm so that M2.5 screws can pass through.
<Outside>
4 x M2.5 x 6 Pozi Pan (DIN7985)
<enclosure>

## Cellular 3.3V -> 5V power.