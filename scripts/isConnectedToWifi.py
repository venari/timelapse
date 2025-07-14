import subprocess

def is_connected_to_wifi_linux():
    try:
        # Use 'iwgetid' to get the ESSID (network name) of a connected Wi-Fi network
        # or 'ifconfig wlan0' to check the status of the wireless interface
        result = subprocess.run(['iwgetid'], capture_output=True, text=True, check=True)
        output = result.stdout

        # If iwgetid returns output, it indicates a connection
        if output.strip():
            return True
        else:
            return False
    except subprocess.CalledProcessError:
        # iwgetid will return an error if not connected to Wi-Fi
        return False
    except FileNotFoundError:
        print("iwgetid command not found. Ensure it's installed (e.g., 'sudo apt install wireless-tools').")
        return False

if is_connected_to_wifi_linux():
    print("Connected to a wireless network (Linux/macOS).")
else:
    print("Not connected to a wireless network (Linux/macOS).")

def wifiSSID():
    try:
        # Use 'iwgetid' to get the ESSID (network name) of the connected Wi-Fi network
        result = subprocess.run(['iwgetid', '-r'], capture_output=True, text=True, check=True)
        return result.stdout.strip()
    except subprocess.CalledProcessError:
        return None
    except FileNotFoundError:
        print("iwgetid command not found. Ensure it's installed (e.g., 'sudo apt install wireless-tools').")
        return None