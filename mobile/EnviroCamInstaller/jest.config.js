module.exports = {
  preset: '@react-native/jest-preset',
  passWithNoTests: true,
  transformIgnorePatterns: [
    'node_modules/(?!(react-native|@react-native|react-native-ble-plx|react-native-wifi-reborn)/)',
  ],
};
