import pytest
import requests

from worker.api_client import OrderAlreadyCompleted, OrdersApiClient


class FakeResponse:
    def __init__(self, status_code=200, body=None):
        self.status_code = status_code
        self.body = body

    def json(self):
        return self.body

    def raise_for_status(self):
        if self.status_code >= 400:
            raise requests.HTTPError(f"HTTP {self.status_code}")


class FakeSession:
    """Records requests instead of sending them."""

    def __init__(self, response):
        self.response = response
        self.calls = []

    def get(self, url, **kwargs):
        self.calls.append(("GET", url, kwargs))
        return self.response

    def post(self, url, **kwargs):
        self.calls.append(("POST", url, kwargs))
        return self.response


def test_claim_order_sends_worker_name_and_returns_order():
    session = FakeSession(FakeResponse(body={"id": 1, "status": "Processing"}))
    client = OrdersApiClient("http://api:8080/", session=session)

    assert client.claim_order("worker-1") == {"id": 1, "status": "Processing"}
    method, url, kwargs = session.calls[0]
    assert (method, url) == ("POST", "http://api:8080/api/orders/claim")
    assert kwargs["json"] == {"processedBy": "worker-1"}


def test_claim_order_returns_none_when_nothing_to_do():
    client = OrdersApiClient("http://api:8080", session=FakeSession(FakeResponse(status_code=204)))

    assert client.claim_order("worker-1") is None


def test_complete_order_sends_worker_name():
    session = FakeSession(FakeResponse(body={"id": 7, "status": "Completed"}))
    client = OrdersApiClient("http://api:8080", session=session)

    client.complete_order(7, "worker-1")

    method, url, kwargs = session.calls[0]
    assert (method, url) == ("POST", "http://api:8080/api/orders/7/complete")
    assert kwargs["json"] == {"processedBy": "worker-1"}


def test_complete_order_conflict_raises_already_completed():
    client = OrdersApiClient("http://api:8080", session=FakeSession(FakeResponse(status_code=409)))

    with pytest.raises(OrderAlreadyCompleted):
        client.complete_order(7, "worker-1")


def test_server_error_raises_http_error():
    client = OrdersApiClient("http://api:8080", session=FakeSession(FakeResponse(status_code=500)))

    with pytest.raises(requests.HTTPError):
        client.claim_order("worker-1")
