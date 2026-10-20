"""Small HTTP client for the Orders API."""

import requests


class OrderAlreadyCompleted(Exception):
    """Another worker completed the order first (API returned 409)."""


class OrdersApiClient:
    def __init__(self, base_url, session=None, timeout=5):
        self.base_url = base_url.rstrip("/")
        self.session = session or requests.Session()
        self.timeout = timeout

    def claim_order(self, worker_name):
        """Ask the API for the next order to work on. Returns the order, or None when there is nothing to do.

        The API hands each order to exactly one worker, so several replicas never do the same work.
        """
        response = self.session.post(
            f"{self.base_url}/api/orders/claim",
            json={"processedBy": worker_name},
            timeout=self.timeout,
        )
        if response.status_code == 204:
            return None
        response.raise_for_status()
        return response.json()

    def complete_order(self, order_id, processed_by):
        response = self.session.post(
            f"{self.base_url}/api/orders/{order_id}/complete",
            json={"processedBy": processed_by},
            timeout=self.timeout,
        )
        if response.status_code == 409:
            raise OrderAlreadyCompleted(f"Order {order_id} was already completed")
        response.raise_for_status()
        return response.json()
