#!/bin/bash

if ! grep -q "bookworm" /etc/os-release; then
    echo "This script is intended for Debian Bookworm only."
    exit 1
fi

declare -A wifi_networks=(
    [SSID1]=PSK1
    [SSID2]=PSK2
)

for ssid in "${!wifi_networks[@]}"; do
    psk="${wifi_networks[$ssid]}"
    sudo nmcli connection add type wifi con-name "$ssid" ssid "$ssid" 802-11-wireless-security.key-mgmt WPA-PSK 802-11-wireless-security.psk "$psk"
done

echo "All Wi-Fi networks configured successfully."