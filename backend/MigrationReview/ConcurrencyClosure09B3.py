"""09B.3 service-order concurrency checks. Uses only stdlib and local psql/pgpass."""
import os
import subprocess
import time
import uuid

DB = 'ashenfall_09b3_test'
PSQL = r'C:\Program Files\PostgreSQL\18\bin\psql.exe'
BASE = [PSQL, '-X', '-w', '-qAt', '-v', 'ON_ERROR_STOP=1', '-h', 'localhost',
        '-p', '5432', '-U', 'postgres', '-d', DB]
CLASS = '90000000-0000-0000-0000-000000000002'
ONE = '90000000-0000-0000-0000-000000000011'
STACK = '90000000-0000-0000-0000-000000000015'


def query(sql):
    p = subprocess.run(BASE, input=sql, text=True, capture_output=True, timeout=15)
    if p.returncode:
        raise RuntimeError(p.stderr)
    return p.stdout.strip()


def connection(name):
    env = os.environ.copy()
    env['PGAPPNAME'] = name
    return subprocess.Popen(BASE, stdin=subprocess.PIPE, stdout=subprocess.PIPE,
                            stderr=subprocess.PIPE, text=True, bufsize=1, env=env)


def send(proc, sql):
    proc.stdin.write(sql + '\n')
    proc.stdin.flush()


def race(label, first, second, expected_state, expected_error, expected_message):
    a, b = connection('ash09b3_a_' + label), connection('ash09b3_b_' + label)
    try:
        send(a, "\\set VERBOSITY verbose\nBEGIN; SET LOCAL lock_timeout='8s'; SET LOCAL statement_timeout='12s'; " + first + " SELECT 'A_READY';")
        while True:
            line = a.stdout.readline()
            if not line:
                raise RuntimeError('A failed before synchronization: ' + a.stderr.read())
            if line.strip() == 'A_READY':
                break
        send(b, "\\set VERBOSITY verbose\nBEGIN; SET LOCAL lock_timeout='8s'; SET LOCAL statement_timeout='12s'; " + second + ' COMMIT;')
        deadline = time.monotonic() + 5
        while time.monotonic() < deadline:
            waiting = query("SELECT count(*) FROM pg_stat_activity WHERE application_name='ash09b3_b_" + label + "' AND wait_event_type='Lock';")
            if waiting == '1':
                break
            if b.poll() is not None:
                raise RuntimeError('B exited before overlapping: ' + b.stderr.read())
            time.sleep(.05)
        else:
            raise RuntimeError('B did not block on A')
        send(a, 'COMMIT; \\q')
        a.stdin.close()
        a.wait(timeout=12)
        b.stdin.close()
        b.wait(timeout=12)
        err_a, err_b = a.stderr.read(), b.stderr.read()
        if (a.returncode != 0 or b.returncode == 0 or expected_error not in err_b
                or expected_message not in err_b):
            raise RuntimeError(f'{label} unexpected result A={a.returncode} {err_a} B={b.returncode} {err_b}')
        state = query(STATE)
        if state != expected_state:
            raise RuntimeError(f'{label} invalid state {state!r}, expected {expected_state!r}')
        print(f'{label}: A committed; B {expected_error}; observed lock wait; final {state}; PASS')
    finally:
        for p in (a, b):
            if p.poll() is None:
                p.kill()
                p.wait()


if query('SELECT current_database();') != DB:
    raise SystemExit('wrong database')
if query("SELECT count(*) FROM accounts;") != '0':
    raise SystemExit('unexpected existing accounts')
account, char, first, second, stack = [str(uuid.uuid4()) for _ in range(5)]
STATE = f"SELECT c.row_revision || '|' || coalesce((SELECT string_agg(e.slot_code || ':' || CASE WHEN e.owned_item_id='{first}' THEN 'one' ELSE 'two' END,',' ORDER BY e.slot_code) FROM character_equipment e WHERE e.character_id=c.id),'empty') || '|' || (SELECT quantity || ':' || row_revision FROM owned_items WHERE id='{stack}') || '|' || (SELECT count(*) || ':' || min(quantity) || ':' || max(quantity) || ':' || min(row_revision) || ':' || max(row_revision) FROM owned_items WHERE id IN ('{first}','{second}')) FROM player_characters c WHERE c.id='{char}';"
lock = f"SELECT id FROM player_characters WHERE id='{char}' FOR UPDATE;"
rev = f"UPDATE player_characters SET row_revision=row_revision+1 WHERE id='{char}';"
stale = f"DO $$ BEGIN IF (SELECT row_revision FROM player_characters WHERE id='{char}') <> 1 THEN RAISE EXCEPTION 'stale character' USING ERRCODE='40001'; END IF; END $$;"
try:
    query(f"INSERT INTO accounts(id,identity_provider,provider_subject) VALUES('{account}','09b3closure','{account}'); INSERT INTO player_characters(id,account_id,class_definition_id,character_name) VALUES('{char}','{account}','{CLASS}','Race Test'); INSERT INTO owned_items(id,character_id,item_definition_id,quantity) VALUES('{first}','{char}','{ONE}',1),('{second}','{char}','{ONE}',1),('{stack}','{char}','{STACK}',2);")
    reset = f"DELETE FROM character_equipment WHERE character_id='{char}'; UPDATE player_characters SET row_revision=row_revision+1 WHERE id='{char}';"
    # Each scenario starts with a fresh revision; derive its value from the database.
    def start():
        return int(query(f"SELECT row_revision FROM player_characters WHERE id='{char}';"))
    def guard(n):
        return f"DO $$ BEGIN IF (SELECT row_revision FROM player_characters WHERE id='{char}') <> {n} THEN RAISE EXCEPTION 'stale character' USING ERRCODE='40001'; END IF; END $$;"
    n=start()
    race('same_slot', lock + f"INSERT INTO character_equipment(character_id,slot_code,owned_item_id) VALUES('{char}','main_hand','{first}');" + rev,
         lock + guard(n) + f"UPDATE character_equipment SET owned_item_id='{second}' WHERE character_id='{char}' AND slot_code='main_hand';" + rev, f'{n+1}|main_hand:one|2:1|2:1:1:1:1', '40001', 'stale character')
    n=start()
    race('equip_unequip', lock + f"UPDATE character_equipment SET owned_item_id='{second}' WHERE character_id='{char}' AND slot_code='main_hand';" + rev,
         lock + guard(n) + f"DELETE FROM character_equipment WHERE character_id='{char}' AND slot_code='main_hand';" + rev, f'{n+1}|main_hand:two|2:1|2:1:1:1:1', '40001', 'stale character')
    query(reset)
    n=start()
    race('equip_quantity', lock + f"INSERT INTO character_equipment(character_id,slot_code,owned_item_id) VALUES('{char}','main_hand','{first}');" + rev,
         lock + f"DO $$ BEGIN IF EXISTS(SELECT 1 FROM character_equipment WHERE owned_item_id='{first}') THEN RAISE EXCEPTION 'equipped' USING ERRCODE='23514'; END IF; END $$; UPDATE owned_items SET quantity=1,row_revision=2 WHERE id='{first}' AND row_revision=1;" + rev,
         f'{n+1}|main_hand:one|2:1|2:1:1:1:1', '23514', 'equipped')
    n=start()
    # Existing equipped item is deliberately replaced before the delete attempts it.
    race('equip_delete', lock + f"UPDATE character_equipment SET owned_item_id='{second}' WHERE character_id='{char}' AND slot_code='main_hand';" + rev,
         lock + f"DELETE FROM owned_items WHERE id='{second}' AND row_revision=1;" + rev,
         f'{n+1}|main_hand:two|2:1|2:1:1:1:1', 'P0001', 'unequip item before deleting it')
    n=start()
    race('stale_item', lock + f"UPDATE owned_items SET quantity=3,row_revision=2 WHERE id='{stack}' AND row_revision=1;" + rev,
         lock + f"DO $$ BEGIN IF (SELECT row_revision FROM owned_items WHERE id='{stack}') <> 1 THEN RAISE EXCEPTION 'stale item' USING ERRCODE='40001'; END IF; END $$; UPDATE owned_items SET quantity=4,row_revision=2 WHERE id='{stack}';" + rev,
         f'{n+1}|main_hand:two|3:2|2:1:1:1:1', '40001', 'stale item')
finally:
    query(f"DELETE FROM character_equipment WHERE character_id='{char}'; DELETE FROM owned_items WHERE character_id='{char}'; DELETE FROM player_characters WHERE id='{char}' AND account_id='{account}'; DELETE FROM accounts WHERE id='{account}' AND identity_provider='09b3closure';")
    print(f'cleanup account {account}: done')
