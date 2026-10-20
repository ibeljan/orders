"""Entry point: poll the Orders API and process pending orders until stopped."""

import logging
import os
import signal
import socket
import time
from pathlib import Path

import requests

from worker.api_client import OrdersApiClient
from worker.processor import process_pending_orders

log = logging.getLogger("worker")


class Config:
    def __init__(self, env=os.environ):
        self.api_url = env.get("API_URL", "http://localhost:8080")
        self.poll_interval = float(env.get("POLL_INTERVAL_SECONDS", "5"))
        self.processing_seconds = float(env.get("PROCESSING_SECONDS", "1"))
        # Hostname = container ID in Docker, pod name in Kubernetes.
        self.worker_name = env.get("WORKER_NAME") or socket.gethostname()
        # Touched on every loop; Docker/Kubernetes can check its age to see if the worker is alive.
        self.heartbeat_file = Path(env.get("HEARTBEAT_FILE", "/tmp/worker-heartbeat"))


class Worker:
    def __init__(self, config, client):
        self.config = config
        self.client = client
        self.running = True

    def stop(self, signum=None, frame=None):
        # Kubernetes sends SIGTERM before stopping a pod: finish the current order, then exit cleanly.
        log.info("Received stop signal, shutting down after the current order")
        self.running = False

    def simulate_work(self, order):
        time.sleep(self.config.processing_seconds)

    def run_once(self):
        try:
            process_pending_orders(self.client, self.config.worker_name, self.simulate_work,
                                   should_continue=lambda: self.running)
        except requests.RequestException as ex:
            # API not reachable (yet): log and try again next loop instead of crashing.
            log.warning("Orders API not reachable at %s: %s", self.config.api_url, ex)
        self.config.heartbeat_file.touch()

    def run(self):
        log.info("Worker %s started, polling %s every %ss",
                 self.config.worker_name, self.config.api_url, self.config.poll_interval)
        while self.running:
            self.run_once()
            # Sleep in small steps so a stop signal is handled quickly.
            deadline = time.monotonic() + self.config.poll_interval
            while self.running and time.monotonic() < deadline:
                time.sleep(0.2)
        log.info("Worker stopped")


def main():
    logging.basicConfig(
        level=os.environ.get("LOG_LEVEL", "INFO"),
        format="%(asctime)s %(levelname)s %(name)s: %(message)s",
    )
    config = Config()
    worker = Worker(config, OrdersApiClient(config.api_url))
    signal.signal(signal.SIGTERM, worker.stop)
    signal.signal(signal.SIGINT, worker.stop)
    worker.run()


if __name__ == "__main__":
    main()
