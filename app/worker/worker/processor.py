"""Business logic: process pending orders. Kept free of HTTP and sleeps so it is easy to unit test."""

import logging

from worker.api_client import OrderAlreadyCompleted

log = logging.getLogger(__name__)


def process_pending_orders(client, worker_name, do_work, should_continue=lambda: True):
    """Claim and process orders one by one until none are left. Returns the number this worker completed.

    do_work(order) is the actual "work" (simulated with a sleep in production, a no-op in tests).
    should_continue() lets a stop signal end the loop between two orders.
    """
    completed = 0

    while should_continue():
        order = client.claim_order(worker_name)
        if order is None:
            break
        log.info("Processing order %s: %s x %s", order["id"], order["quantity"], order["product"])
        do_work(order)
        try:
            client.complete_order(order["id"], worker_name)
            completed += 1
            log.info("Completed order %s", order["id"])
        except OrderAlreadyCompleted:
            # Only happens if our claim expired (we were too slow) and another worker finished it.
            log.info("Order %s was already completed by another worker, skipping", order["id"])

    return completed
