import socket
import threading

def handle_conn(conn: socket.socket, addr):
    print(f'connection from addr: {addr}')
    while 1:
        data = conn.recv(1024)
        print(data)

s = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
s.bind(('localhost', 8000))
s.listen(3)
while 1:
    conn, addr = s.accept()
    threading.Thread(target=handle_conn, args=[conn, addr], daemon=True).start()