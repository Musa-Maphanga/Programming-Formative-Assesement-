RaceDay is an event management system for road running, walking and cycling events in South Africa. Organisers can set up events and capture results, and participants can enter events and keep track of how they did.

This repository holds my Part 1 planning work. The API and front end get built in Part 2.

# The database

The ERD has seven tables:

  ------------------ --------------------------------------------------------------------
  **Table**          **What it holds**

  Roles              The two user roles, Organiser and Participant

  Users              Everyone who signs up, with their role

  EventTypes         Run, Walk or Cycle

  Events             The events that organisers create

  Categories         The age or distance groups for each event, like Under 20 or 21km

  Enrolments         Which participant entered which event, and the category they chose

  Results            Finish time and position for an enrolment
  ------------------ --------------------------------------------------------------------

Participants and Events are a many-to-many relationship, because one participant can enter many events and one event has many participants. Enrolments is the table in the middle that sorts this out. It also stores

# Why I designed it this way

**Roles and event types are their own tables.** I could have stored these as text in the Users and Events tables, but then a typo like \"Organizer\" would break things. Having them as lookup tables means the foreign key only allows valid values, and new event types can be added later without changing the schema.

**A category must belong to its event.** If Enrolments only had a normal foreign key to Categories, a participant could enter the Durban half marathon but be linked to a category from a different event. To stop that, Categories has a unique constraint on (CategoryId, EventId), and Enrolments uses a composite foreign key on those two columns. Now the database itself refuses a category that does not belong to the event.

**One entry per participant per event.** Enrolments has a unique constraint on (ParticipantId, EventId), so nobody can enter the same event twice by accident.

**One result per enrolment.** EnrolmentId is unique in Results, which makes it a one-to-zero-or-one relationship. An enrolment only gets a result once the event has been run.

**Latitude and longitude on Events.** Part 2 needs live weather for race day, and weather APIs work off coordinates, so I store them with the event.

**No cascade deletes.** Deleting an event that people have already entered would wipe their enrolments and results, which is not something I want to happen by accident. The foreign keys use NO ACTION instead, so SQL Server blocks the delete. The API returns 409 Conflict in that case and the organiser cancels the event instead by setting its status to Cancelled.

**The database does not check that an organiser is really an Organiser.** Events.OrganiserId points to Users, but a foreign key cannot check the user's role. Enforcing that in the database would need a trigger or a computed column, which felt like overkill for this system. The API checks the role from the JWT token before it lets anyone create an event, so the rule lives in the code instead. I decided this is the one rule where the check sits outside the database.

# Differences between the ERD and the SQL script

The SQL script matches the ERD exactly. The only thing the diagram does not show is the data type sizes, like NVARCHAR(150), because that would have made it too cramped to read. The script also has CHECK constraints (distances must be positive, statuses must be from a fixed list, MinAge cannot be more than MaxAge) which are not drawn on the ERD.

# Running the SQL script

1.  Open docs/RaceDay_Schema.sql in SQL Server Management Studio.

2.  Connect to your local instance and press F5 to run the whole script.

3.  The script creates the RaceDay database, builds the seven tables and inserts sample data.

The script drops the tables first if they exist, so it can be run more than once without errors. At the end it runs a query that shows the row counts, which should be:

  -----------------------------------------------------------------------
  **Table**                                    **Rows**
  -------------------------------------------- --------------------------
  Roles                                        2

  Users                                        5

  EventTypes                                   3

  Events                                       3

  Categories                                   8

  Enrolments                                   6

  Results                                      3
  -----------------------------------------------------------------------

## Sample data

Two organisers, three participants and three events. One of the events has already taken place so there are results to show, and the other two are still coming up so they can be entered. Every participant's age fits the category they are entered in.

The password for all the sample accounts is Password123! It is stored as a BCrypt hash, not plain text, so it will work with BCrypt.Net-Next in Part 2.

# API endpoint plan

The full plan covers everything in the Functional Requirements (authentication, user profiles, events, categories, enrolments and results) and a few extras I added: changing a password, fetching the list of event types, and getting the weather for an event.

The API uses JWT tokens. The token holds the user's ID and role, and endpoints check the role before doing a
