import requests
import json

config = json.load(open('config.json'))
localConfig = json.load(open('localConfig.json'))

with open('./lastImage.path', 'r') as f:
    lastImagePath = f.read()

files = {
    'File': open(lastImagePath, 'rb'),
}

data = {
    'DeviceId': localConfig['deviceId']
}

print('data:')
print(data)

session = requests.Session()
response = session.post(config['apiUrl'] + 'Image', files=files, data=data)

print(f'Response code: {response.status_code}')
print(f'Response text:')
try:
    print(json.dumps(json.loads(response.text), indent = 4))
except json.decoder.JSONDecodeError:
    print(response.text)
