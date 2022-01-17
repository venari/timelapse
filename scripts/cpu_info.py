import cpuinfo 
import multiprocessing

# from cpuinfo import get_cpu_info

# # info = get_cpu_info()
# # info = cpuinfo.get_cpu_info()
# info = cpuinfo._get_cpu_info_from_kstat()
# print(info)

# print(multiprocessing.cpu_count())


# import platform

# print(platform.version)
# print(platform.machine())
# print(platform.version())
# print(platform.platform())
# print(platform.uname())
# print(platform.system())
# print(platform.processor())
# print(platform.machine().)


import platform, os 
 
def cpu_info(): 
    if platform.system() == 'Windows': 
        return platform.processor() 
    elif platform.system() == 'Darwin': 
        command = '/usr/sbin/sysctl -n machdep.cpu.brand_string' 
        return os.popen(command).read().strip() 
    elif platform.system() == 'Linux': 
        command = 'cat /proc/cpuinfo' 
        return os.popen(command).read().strip() 
    return 'platform not identified' 
 
print(cpu_info()) 