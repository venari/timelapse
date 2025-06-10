#!/usr/bin/env python3
# filepath: envirocam_status.py

import os
import glob
import argparse
import subprocess
import json
import datetime
import psutil
from tabulate import tabulate

def get_service_status(service_name):
    """Check if a systemd service is active and enabled."""
    try:
        active_check = subprocess.run(["systemctl", "is-active", service_name], 
                                     capture_output=True, text=True)
        enabled_check = subprocess.run(["systemctl", "is-enabled", service_name], 
                                      capture_output=True, text=True)
        
        active = active_check.stdout.strip()
        enabled = enabled_check.stdout.strip()
        
        return {"active": active, "enabled": enabled}
    except Exception as e:
        return {"active": "error", "enabled": "error", "error": str(e)}

def count_files(pattern):
    """Count files matching a glob pattern."""
    return len(glob.glob(pattern))

def get_latest_file(pattern):
    """Get the most recent file matching a pattern."""
    files = glob.glob(pattern)
    if not files:
        return None
    
    latest = max(files, key=os.path.getmtime)
    timestamp = datetime.datetime.fromtimestamp(os.path.getmtime(latest))
    return {
        "path": latest,
        "filename": os.path.basename(latest),
        "timestamp": timestamp.strftime("%Y-%m-%d %H:%M:%S")
    }

def get_cpu_temp():
    """Get the CPU temperature."""
    try:
        temp = subprocess.run(["vcgencmd", "measure_temp"], 
                             capture_output=True, text=True).stdout
        return temp.strip().replace("temp=", "")
    except:
        return "N/A"

def get_system_info():
    """Get system information."""
    disk = psutil.disk_usage('/')
    memory = psutil.virtual_memory()
    
    return {
        "cpu_temp": get_cpu_temp(),
        "cpu_usage": f"{psutil.cpu_percent()}%",
        "ram_used": f"{memory.percent}% ({memory.used / 1024 / 1024:.1f}MB/{memory.total / 1024 / 1024:.1f}MB)",
        "disk_used": f"{disk.percent}% ({disk.used / 1024 / 1024 / 1024:.1f}GB/{disk.total / 1024 / 1024 / 1024:.1f}GB)",
        "uptime": subprocess.run(["uptime", "-p"], capture_output=True, text=True).stdout.strip()
    }

def main():
    parser = argparse.ArgumentParser(description="Check status of Raspberry Pi EnviroCam system")
    parser.add_argument("--detailed", "-d", action="store_true", help="Show detailed information")
    parser.add_argument("--services", "-s", action="store_true", help="Show service status")
    args = parser.parse_args()

    # Adjust these paths to match your system
    PENDING_IMAGES_PATTERN = "/home/pi/dev/timelapse/pending_images/*.jpg"
    UPLOADED_IMAGES_PATTERN = "/home/pi/dev/timelapse/uploaded_images/*.jpg"
    PENDING_TELEMETRY_PATTERN = "/home/pi/dev/timelapse/pending_telemetry/*.json"
    UPLOADED_TELEMETRY_PATTERN = "/home/pi/dev/timelapse/uploaded_telemetry/*.json"
    
    # Count files
    pending_images = count_files(PENDING_IMAGES_PATTERN)
    uploaded_images = count_files(UPLOADED_IMAGES_PATTERN)
    pending_telemetry = count_files(PENDING_TELEMETRY_PATTERN)
    uploaded_telemetry = count_files(UPLOADED_TELEMETRY_PATTERN)
    
    # Get latest files
    latest_image = get_latest_file(UPLOADED_IMAGES_PATTERN)
    latest_telemetry = get_latest_file(UPLOADED_TELEMETRY_PATTERN)
    
    # System info
    sys_info = get_system_info()
    
    # Print report
    print("\n📊 RASPBERRY PI ENVIROCAM STATUS REPORT 📊")
    print("=" * 50)
    
    print(f"\n🖼️  Images:")
    print(f"   Pending upload: {pending_images}")
    print(f"   Uploaded: {uploaded_images}")
    if latest_image:
        print(f"   Latest upload: {latest_image['filename']} ({latest_image['timestamp']})")
    
    print(f"\n📡 Telemetry:")
    print(f"   Pending upload: {pending_telemetry}")
    print(f"   Uploaded: {uploaded_telemetry}")
    if latest_telemetry:
        print(f"   Latest upload: {latest_telemetry['filename']} ({latest_telemetry['timestamp']})")
        
        if args.detailed and latest_telemetry:
            try:
                with open(latest_telemetry['path'], 'r') as f:
                    data = json.load(f)
                    print("\n   Latest telemetry data:")
                    for key, value in data.items():
                        print(f"     {key}: {value}")
            except:
                print("   Could not parse latest telemetry data")
    
    print(f"\n💻 System:")
    print(f"   CPU temp: {sys_info['cpu_temp']}")
    print(f"   CPU usage: {sys_info['cpu_usage']}")
    print(f"   RAM: {sys_info['ram_used']}")
    print(f"   Disk: {sys_info['disk_used']}")
    print(f"   Uptime: {sys_info['uptime']}")
    
    if args.services:
        print("\n🔌 Services:")
        services = [
            "envirocam-logging.service",
            "envirocam-telemetry.service",
            "envirocam-photos.service",
            "envirocam-upload.service",
            "envirocam-photos.timer",
            "envirocam-upload.timer",
            "envirocam-detect-hang.timer"
        ]
        
        service_data = []
        for service in services:
            status = get_service_status(service)
            service_data.append([service, status['active'], status['enabled']])
        
        print(tabulate(service_data, headers=["Service", "Status", "Enabled"]))

if __name__ == "__main__":
    main()