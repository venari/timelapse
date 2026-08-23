import React, {useEffect, useRef, useState} from 'react';
import {
  ActivityIndicator,
  FlatList,
  Image,
  Pressable,
  SafeAreaView,
  StyleSheet,
  Text,
  View,
} from 'react-native';
import {BleManager, Device} from 'react-native-ble-plx';
import WifiManager from 'react-native-wifi-reborn';

// Mirrors esp32/envirocam/main.cpp: SETUP_AP_SSID_PREFIX / SETUP_AP_PASSWORD. The AP
// password is a fixed shared constant baked into the firmware (not a per-device
// secret), so BLE only needs to tell us which camera's SSID to join, not the password.
const SSID_PREFIX = 'EnviroCam-';
const AP_PASSWORD = 'envirocam';
// Default softAP gateway for the ESP32 Arduino core - the firmware never overrides it
// via softAPConfig(). Re-verify against a real device before relying on this.
const STREAM_URL = 'http://192.168.4.1/stream';

type Screen =
  | {kind: 'scanning'}
  | {kind: 'joining'; ssid: string}
  | {kind: 'preview'; ssid: string}
  | {kind: 'error'; message: string};

export default function App(): React.JSX.Element {
  const bleManager = useRef(new BleManager()).current;
  const [devices, setDevices] = useState<Device[]>([]);
  const [screen, setScreen] = useState<Screen>({kind: 'scanning'});

  useEffect(() => {
    const subscription = bleManager.onStateChange(state => {
      if (state === 'PoweredOn') {
        startScan();
      }
    }, true);
    return () => {
      subscription.remove();
      bleManager.stopDeviceScan();
      bleManager.destroy();
    };
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  function startScan() {
    setDevices([]);
    bleManager.startDeviceScan(null, null, (error, device) => {
      if (error) {
        setScreen({kind: 'error', message: error.message});
        return;
      }
      if (!device?.name?.startsWith(SSID_PREFIX)) {
        return;
      }
      setDevices(prev =>
        prev.some(d => d.id === device.id) ? prev : [...prev, device],
      );
    });
  }

  async function joinDevice(ssid: string) {
    bleManager.stopDeviceScan();
    setScreen({kind: 'joining', ssid});
    try {
      await WifiManager.connectToProtectedSSID(ssid, AP_PASSWORD, false, false);
      setScreen({kind: 'preview', ssid});
    } catch {
      setScreen({
        kind: 'error',
        message:
          `Could not join "${ssid}" automatically. You can still connect manually: ` +
          `open Settings > WiFi, join "${ssid}" with password "${AP_PASSWORD}", ` +
          `then open ${STREAM_URL} in Safari.`,
      });
    }
  }

  if (screen.kind === 'preview') {
    return (
      <SafeAreaView style={styles.container}>
        <Text style={styles.header}>{screen.ssid}</Text>
        <Image source={{uri: STREAM_URL}} style={styles.preview} resizeMode="contain" />
      </SafeAreaView>
    );
  }

  if (screen.kind === 'joining') {
    return (
      <SafeAreaView style={[styles.container, styles.centered]}>
        <ActivityIndicator size="large" />
        <Text style={styles.status}>Joining {screen.ssid}...</Text>
      </SafeAreaView>
    );
  }

  if (screen.kind === 'error') {
    return (
      <SafeAreaView style={[styles.container, styles.centered]}>
        <Text style={styles.status}>{screen.message}</Text>
        <Pressable style={styles.button} onPress={() => setScreen({kind: 'scanning'})}>
          <Text style={styles.buttonText}>Try again</Text>
        </Pressable>
      </SafeAreaView>
    );
  }

  return (
    <SafeAreaView style={styles.container}>
      <Text style={styles.header}>Nearby cameras</Text>
      <FlatList
        data={devices}
        keyExtractor={item => item.id}
        renderItem={({item}) => (
          <Pressable style={styles.row} onPress={() => joinDevice(item.name!)}>
            <Text style={styles.rowTitle}>{item.name}</Text>
            <Text style={styles.rowSubtitle}>RSSI {item.rssi ?? '?'}</Text>
          </Pressable>
        )}
        ListEmptyComponent={
          <View style={styles.centered}>
            <ActivityIndicator />
            <Text style={styles.status}>Scanning for cameras...</Text>
          </View>
        }
      />
    </SafeAreaView>
  );
}

const styles = StyleSheet.create({
  container: {flex: 1, backgroundColor: '#fff'},
  centered: {alignItems: 'center', justifyContent: 'center', flex: 1, gap: 12},
  header: {fontSize: 20, fontWeight: '600', padding: 16},
  status: {fontSize: 15, color: '#444', textAlign: 'center', paddingHorizontal: 24},
  preview: {flex: 1, backgroundColor: '#000'},
  row: {
    paddingVertical: 14,
    paddingHorizontal: 16,
    borderBottomWidth: StyleSheet.hairlineWidth,
    borderBottomColor: '#ddd',
  },
  rowTitle: {fontSize: 17},
  rowSubtitle: {fontSize: 13, color: '#888', marginTop: 2},
  button: {
    marginTop: 16,
    backgroundColor: '#007aff',
    paddingVertical: 10,
    paddingHorizontal: 20,
    borderRadius: 8,
  },
  buttonText: {color: '#fff', fontSize: 16, fontWeight: '600'},
});
