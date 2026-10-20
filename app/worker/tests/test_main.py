import requests

from worker.main import Config, Worker


class UnreachableClient:
    def claim_order(self, worker_name):
        raise requests.ConnectionError("connection refused")


class EmptyClient:
    def claim_order(self, worker_name):
        return None


def test_config_defaults():
    config = Config(env={})

    assert config.api_url == "http://localhost:8080"
    assert config.poll_interval == 5
    assert config.worker_name  # falls back to the hostname


def test_config_from_environment():
    config = Config(env={"API_URL": "http://api:8080", "POLL_INTERVAL_SECONDS": "2", "WORKER_NAME": "w1"})

    assert config.api_url == "http://api:8080"
    assert config.poll_interval == 2
    assert config.worker_name == "w1"


def test_run_once_writes_heartbeat(tmp_path):
    config = Config(env={"HEARTBEAT_FILE": str(tmp_path / "heartbeat")})

    Worker(config, EmptyClient()).run_once()

    assert (tmp_path / "heartbeat").exists()


def test_run_once_survives_unreachable_api(tmp_path):
    config = Config(env={"HEARTBEAT_FILE": str(tmp_path / "heartbeat")})

    Worker(config, UnreachableClient()).run_once()  # must not raise

    assert (tmp_path / "heartbeat").exists()


def test_stop_ends_the_loop():
    worker = Worker(Config(env={}), EmptyClient())

    worker.stop()

    assert worker.running is False
