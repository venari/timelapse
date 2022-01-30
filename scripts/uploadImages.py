import requests
import json

config = json.load(open('config.json'))

with open('./lastImage.path', 'r') as f:
    lastImagePath = f.read()

data = {
    'image': open(lastImagePath, 'rb')
    'deviceId': config['deviceId']
}

session = requests.Session()
session.post(config['apiURL'] + 'Image', data=data)
