"""Drives an MTP test host in server mode, as Visual Studio and C# Dev Kit do.

Usage: python3 -I server_probe.py <test host executable> <uid to run>

It sends initialize, testing/discoverTests and testing/runTests for one UID, and prints the
discovered nodes with their location properties and the states reported for the filtered run.
"""
import json
import socket
import subprocess
import sys
import uuid


def send(conn, message):
    body = json.dumps(message).encode()
    conn.sendall(b"Content-Length: %d\r\n\r\n" % len(body) + body)


def receive(conn, buffer):
    while b"\r\n\r\n" not in buffer:
        chunk = conn.recv(65536)
        if not chunk:
            raise EOFError("host closed the connection")
        buffer += chunk
    header, rest = buffer.split(b"\r\n\r\n", 1)
    length = next(int(line.split(b":")[1]) for line in header.split(b"\r\n") if line.lower().startswith(b"content-length"))
    while len(rest) < length:
        rest += conn.recv(65536)
    return json.loads(rest[:length]), rest[length:]


def call(conn, buffer, request_id, method, params, on_update):
    send(conn, {"jsonrpc": "2.0", "id": request_id, "method": method, "params": params})
    done = False
    result = None
    while not done or on_update.pending:
        message, buffer = receive(conn, buffer)
        if message.get("id") == request_id and "method" not in message:
            if "error" in message:
                raise RuntimeError(message["error"])
            result = message.get("result")
            done = True
        elif message.get("method") == "testing/testUpdates/tests":
            on_update(message["params"])
        elif "id" in message and "method" in message:
            send(conn, {"jsonrpc": "2.0", "id": message["id"], "result": None})
    return result, buffer


class Updates:
    def __init__(self):
        self.nodes = []
        self.pending = False

    def start(self):
        self.pending = True

    def __call__(self, params):
        if params.get("changes") is None:
            self.pending = False
            return
        self.nodes.extend(change["node"] for change in params["changes"])


def main():
    host, uid = sys.argv[1], sys.argv[2]
    listener = socket.socket()
    listener.bind(("127.0.0.1", 0))
    listener.listen(1)
    port = listener.getsockname()[1]
    process = subprocess.Popen([host, "--server", "--client-host", "127.0.0.1", "--client-port", str(port)])
    conn, _ = listener.accept()
    buffer = b""
    updates = Updates()

    result, buffer = call(conn, buffer, 1, "initialize", {
        "processId": 0,
        "clientInfo": {"name": "server_probe", "version": "1.0"},
        "capabilities": {"testing": {"debuggerProvider": False}},
    }, updates)
    print("server:", result["serverInfo"]["name"], result["serverInfo"]["version"])

    updates.start()
    _, buffer = call(conn, buffer, 2, "testing/discoverTests", {"runId": str(uuid.uuid4())}, updates)
    print("discovered:")
    for node in updates.nodes:
        location = {k: v for k, v in node.items() if k.startswith("location.")}
        print("  ", node["uid"], node["execution-state"], json.dumps(location))

    updates = Updates()
    updates.start()
    target = [{"uid": uid, "display-name": uid}]
    _, buffer = call(conn, buffer, 3, "testing/runTests", {"runId": str(uuid.uuid4()), "tests": target}, updates)
    print("run", uid + ":")
    for node in updates.nodes:
        print("  ", node["uid"], node["execution-state"])

    send(conn, {"jsonrpc": "2.0", "method": "exit", "params": {}})
    conn.close()
    print("host exit code:", process.wait(timeout=30))


main()
