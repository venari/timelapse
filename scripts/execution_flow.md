```mermaid
graph TD
    boot --> crontab
    crontab --> saveTelemetry.sh
    crontab --> savePhotos.sh
    crontab --> sleep[sleep 60] --> uploadPending.sh

    subgraph saveTelemetry
    saveTelemetry.sh --> outTel[output to saveTelemetry.sh.out]
    outTel --> saveTelemetry.py
    end

    subgraph savePhotos
    savePhotos.sh --> outPhotos[output to savePhotos.sh.out]
    outPhotos --> savePhotos.py
    end

    subgraph uploadPending
    uploadPending.sh --> outPending[output to uploadPending.sh.out]
    outPending --> uploadPending.py --> upPendingLoop[while True]
    subgraph uploadPendingpy[uploadPending.py]
    upPendingLoop --> loadConfig[load config] --> deleteOld["deleteOldUploadedImagesAndTelemetry()"] --> upPenTel["uploadPendingTelemetry()"] --> upPenPhot["uploadPendingPhotos()"] --> sleep2["time.sleep(30)"]
    sleep2 --> upPendingLoop
    end
    end
```
