from datetime import datetime, timedelta
from zoneinfo import ZoneInfo

from helpers import currentPhase

print(f"currentPhase(): {currentPhase()}")
print(f"currentPhase(): {currentPhase(datetime.utcnow() + timedelta(hours=1))}")
print(f"currentPhase(): {currentPhase(datetime.utcnow() + timedelta(hours=2))}")
print(f"currentPhase(): {currentPhase(datetime.utcnow() + timedelta(hours=3))}")
print(f"currentPhase(): {currentPhase(datetime.utcnow() + timedelta(hours=4))}")
print(f"currentPhase(): {currentPhase(datetime.utcnow() + timedelta(hours=5))}")
print(f"currentPhase(): {currentPhase(datetime.utcnow() + timedelta(hours=6))}")
print(f"currentPhase(): {currentPhase(datetime.utcnow() + timedelta(hours=7))}")
print(f"currentPhase(): {currentPhase(datetime.utcnow() + timedelta(hours=8))}")
print(f"currentPhase(): {currentPhase(datetime.utcnow() + timedelta(hours=9))}")
print(f"currentPhase(): {currentPhase(datetime.utcnow() + timedelta(hours=10))}")
print(f"currentPhase(): {currentPhase(datetime.utcnow() + timedelta(hours=11))}")
print(f"currentPhase(): {currentPhase(datetime.utcnow() + timedelta(hours=12))}")
print(f"currentPhase(): {currentPhase(datetime.utcnow() + timedelta(hours=13))}")
print(f"currentPhase(): {currentPhase(datetime.utcnow() + timedelta(hours=14))}")
print(f"currentPhase(): {currentPhase(datetime.utcnow() + timedelta(hours=15))}")
print(f"currentPhase(): {currentPhase(datetime.utcnow() + timedelta(hours=16))}")
print(f"currentPhase(): {currentPhase(datetime.utcnow() + timedelta(hours=17))}")
print(f"currentPhase(): {currentPhase(datetime.utcnow() + timedelta(hours=18))}")
print(f"currentPhase(): {currentPhase(datetime.utcnow() + timedelta(hours=19))}")
print(f"currentPhase(): {currentPhase(datetime.utcnow() + timedelta(hours=20))}")
print(f"currentPhase(): {currentPhase(datetime.utcnow() + timedelta(hours=21))}")
print(f"currentPhase(): {currentPhase(datetime.utcnow() + timedelta(hours=22))}")
print(f"currentPhase(): {currentPhase(datetime.utcnow() + timedelta(hours=23))}")
print(f"currentPhase(): {currentPhase(datetime.utcnow() + timedelta(hours=24))}")

test = datetime(2024, 12, 2, 5, 30, 00, 00, tzinfo=ZoneInfo('Pacific/Auckland'))
print(test)
print(f"currentPhase(): {currentPhase(test)}")
test = datetime(2024, 12, 1, 16, 30, 00, 00)
print(test)
print(f"currentPhase( 05:30am NZ in GMT): {currentPhase(test)}")

test = datetime(2024, 12, 1, 16, 40, 00, 00)
print(test)
print(f"currentPhase( 05:40am NZ in GMT): {currentPhase(test)}")

test = datetime(2024, 12, 1, 16, 50, 00, 00)
print(test)
print(f"currentPhase( 05:50am NZ in GMT): {currentPhase(test)}")

# print(f"currentPhase(): {currentPhase(datetime.utcnow())}")
# print(f"currentPhase(): {currentPhase(datetime.now())}")
# print(f"currentPhase(): {currentPhase(datetime(2024, 11, 30, 12, 00, 00, 00, tzinfo=ZoneInfo('Pacific/Auckland')))}")
# print(f"currentPhase(): {currentPhase(datetime(2024, 11, 30, 12, 00, 00, 00))}")

