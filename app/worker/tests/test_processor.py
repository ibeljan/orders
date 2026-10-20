from worker.api_client import OrderAlreadyCompleted
from worker.processor import process_pending_orders


class FakeClient:
    """Stands in for the real API client — no HTTP needed. Hands out queued orders one by one."""

    def __init__(self, queue, already_completed=()):
        self.queue = list(queue)
        self.already_completed = set(already_completed)
        self.claimed_by = []
        self.completed = []

    def claim_order(self, worker_name):
        if not self.queue:
            return None
        self.claimed_by.append(worker_name)
        return self.queue.pop(0)

    def complete_order(self, order_id, processed_by):
        if order_id in self.already_completed:
            raise OrderAlreadyCompleted()
        self.completed.append((order_id, processed_by))


def order(order_id, product="Laptop", quantity=1):
    return {"id": order_id, "product": product, "quantity": quantity, "status": "Processing"}


def no_work(order):
    pass


def test_nothing_to_claim_does_nothing():
    client = FakeClient(queue=[])

    assert process_pending_orders(client, "worker-1", no_work) == 0
    assert client.completed == []


def test_completes_every_claimed_order_with_worker_name():
    client = FakeClient(queue=[order(1), order(2)])

    assert process_pending_orders(client, "worker-1", no_work) == 2
    assert client.completed == [(1, "worker-1"), (2, "worker-1")]
    assert client.claimed_by == ["worker-1", "worker-1"]


def test_does_the_work_for_every_order():
    client = FakeClient(queue=[order(1, "Laptop"), order(2, "Monitor")])
    worked_on = []

    process_pending_orders(client, "worker-1", lambda o: worked_on.append(o["product"]))

    assert worked_on == ["Laptop", "Monitor"]


def test_skips_order_completed_by_another_worker():
    client = FakeClient(queue=[order(1), order(2)], already_completed={1})

    assert process_pending_orders(client, "worker-1", no_work) == 1
    assert client.completed == [(2, "worker-1")]


def test_stop_signal_ends_the_loop_between_orders():
    client = FakeClient(queue=[order(1), order(2), order(3)])
    done = []

    def work_then_stop(o):
        done.append(o["id"])

    process_pending_orders(client, "worker-1", work_then_stop, should_continue=lambda: len(done) < 1)

    assert done == [1]
    assert len(client.queue) == 2      # the rest stays for other workers
