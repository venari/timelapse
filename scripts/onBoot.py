import threading

import savePhotos
import saveTelemetry
import uploadPending

threading.Thread(target=savePhotos.main).start()
threading.Thread(target=saveTelemetry.main).start()
threading.Thread(target=uploadPending.main).start()