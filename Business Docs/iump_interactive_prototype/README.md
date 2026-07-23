# IUMP Industrial Light — Interactive P0 Prototype v0.1

A self-contained clickable prototype for the IDEA Utility Monitoring Platform. It demonstrates the selected **Industrial Light** design direction and the primary P0 interaction flows defined in DOC-08.

## Open the prototype

Open `index.html` in Chromium, Chrome, Edge or Firefox. No package installation, build command or backend is required.

For the most reliable local behavior, serve the folder with any static server:

```bash
python -m http.server 8080
```

Then open `http://localhost:8080`.

## Detailed P0 screens

- Application Shell and Site/Area context
- Operations Overview
- Alert Queue
- Alert Detail and simulated state transitions
- Point Detail with History, Data Quality, Rules, Alerts, Source and Audit tabs
- Rule Builder with five steps, test evidence and Submit for Review
- CSV Import Preview, Processing and Result

## Suggested walkthrough

### Alert workflow

1. Open **Overview**.
2. Select `Power exceeds approved limit` in Alert Queue.
3. Click **Acknowledge**.
4. Click **Start work**.
5. Click **Mark recovered** and observe that the case remains open.
6. Click **Resolve after review**, then **Close alert**.
7. Return to Alert Queue and inspect the updated state.

### Point investigation

1. Open the alert's **Point detail**.
2. Inspect the chart, including the Missing-data gap.
3. Open **Data Quality** and **Alerts** tabs.
4. Confirm that Missing intervals never display numeric zero.

### Rule workflow

1. Open **Rules** from the sidebar.
2. Progress through Basic info, Conditions, Settings, Schedule and Review.
3. Run the test.
4. Submit the rule for review.
5. Observe that submission does not activate the rule.

### CSV workflow

1. Open **Imports**.
2. Filter preview rows by Valid, Warning, Invalid or Duplicate.
3. Open row-level reasons.
4. Confirm import of valid rows.
5. Review the local Processing and Result states.

## Responsive behavior

- Desktop-first at 1440 px.
- Collapsed sidebar at narrower desktop widths.
- Mobile navigation below 768 px.
- Alert tables become compact cards on mobile.
- Complex configuration screens remain usable but are not intended as a complete native-mobile experience.

## Prototype rules demonstrated

- Missing data is a state, not a measurement with value `0`.
- Data values display unit, timestamp and quality context.
- Recovered is distinct from Closed.
- Notification delivery is separate from alert ownership.
- Rule test and reviewer submission are distinct from activation.
- CSV Preview is read-only until explicit confirmation.
- All values, users, sites and readings are fictional.

## Limitations

- No backend, authentication, database or API calls.
- State resets after page reload.
- Export and download actions show simulated feedback only.
- Modbus, Edge Collector, AI, Improvement Actions and enterprise integrations are outside this prototype.
- Non-P0 menu destinations use a deliberate placeholder state.
