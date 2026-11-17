-- No name, email, IP, machine ID or per-day activity history is retained.
CREATE TABLE registrations (
  id TEXT PRIMARY KEY,
  email_key TEXT NOT NULL,
  token_hash TEXT NOT NULL,
  created_at INTEGER NOT NULL,
  last_seen INTEGER NOT NULL,
  version TEXT NOT NULL
);
CREATE INDEX registrations_email ON registrations(email_key);
CREATE INDEX registrations_activity ON registrations(last_seen);
