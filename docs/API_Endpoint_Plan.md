# RaceDay — API Endpoint Plan

**Author:** Musa Maphanga · **Student number:** ST10392587

This is the plan from Part 1, updated to match what the API actually does now that
Part 2 is finished. Every route below exists in `src/RaceDay.Api` and appears in
Swagger when the API is running.

---

## Conventions

| Item | Decision |
|---|---|
| Base URL | Everything sits under `/api` |
| Authentication | A JWT bearer token in the `Authorization` header. The token points at a server-side session, so logging out really does end it |
| Roles | `Organiser` and `Participant`, chosen at registration. "None" means no token is needed, "Any" means any signed-in user |
| Owner rule | Where a row says *(owner)*, the caller has to be the organiser who created the event or the participant who made the entry. Anyone else gets 403 |
| Errors | `ProblemDetails`, or `ValidationProblemDetails` with a per-field `errors` object when model validation fails |
| Common failures | 401 missing or expired token, 403 wrong role or not the owner, 404 unknown id, 400 invalid fields |
| Dates | `eventDate` carries the date and the start time together. Finish times are `HH:MM:SS` |

---

## 1. Auth

| Method | Route | Description | Role | Request body | Response |
|---|---|---|---|---|---|
| POST | `/api/auth/register` | Creates an account as either an Organiser or a Participant. The role cannot be changed afterwards. | None | `firstName, lastName, email, password, role, phoneNumber, dateOfBirth, gender, city` | `201` with the new profile. `400` unknown role or weak password. `409` email already registered |
| POST | `/api/auth/login` | Checks the email and password and starts a session. | None | `email, password` | `200` with `{ token, expiresAt, userId, role }`. `401` wrong credentials |
| POST | `/api/auth/logout` | Ends the server-side session the token points at. | Any | None | `204` |

## 2. User profile

| Method | Route | Description | Role | Request body | Response |
|---|---|---|---|---|---|
| GET | `/api/users/me` | The signed-in user's own profile. The password hash is never returned. | Any | None | `200` with the profile |
| PUT | `/api/users/me` | Updates the caller's own details. Email and role cannot be changed here. | Any | `firstName, lastName, phoneNumber, dateOfBirth, gender, city` | `200` with the updated profile |
| PUT | `/api/users/me/password` | Changes the password after checking the current one. | Any | `currentPassword, newPassword` | `204`. `401` current password wrong |
| POST | `/api/users/me/profile-picture` | Uploads a profile picture as `multipart/form-data` under the field name `file`. Stored in Azure Blob Storage by the API. | Any | The image file | `200` with `{ url }`. `400` not an acceptable image. `503` storage not configured |

## 3. Event types

| Method | Route | Description | Role | Request body | Response |
|---|---|---|---|---|---|
| GET | `/api/event-types` | Run, Walk or Cycle. Fills the discipline dropdown. | Any | None | `200` with the list |

## 4. Events

| Method | Route | Description | Role | Request body | Response |
|---|---|---|---|---|---|
| GET | `/api/events` | Browse events, filtered by `type`, `city`, `province`, `from`, `to` and `status`. Sorted by date. | Any | None | `200` with the list |
| GET | `/api/events/{id}` | One event with its categories. | Any | None | `200` with the event. `404` unknown event |
| GET | `/api/users/me/events` | The organiser's own events, drafts and past ones included. | Organiser | None | `200` with the list |
| POST | `/api/events` | Creates an event. The organiser comes from the token, never the body, and the stored role is checked before saving. | Organiser | `name, description, eventDate, location, city, province, latitude, longitude, distanceKm, eventTypeId, maxParticipants` | `201` with the event. `400` date in the past. `404` unknown event type |
| PUT | `/api/events/{id}` | Updates the event, including its status. | Organiser (owner) | Same as POST plus `status` | `200`. `400` invalid status or a limit below the number already entered. `403` not the owner |
| DELETE | `/api/events/{id}` | Deletes an event that nobody has entered. Its categories go with it. | Organiser (owner) | None | `204`. `409` people have already entered, so cancel it instead |
| POST | `/api/events/{id}/banner` | Uploads the banner image as `multipart/form-data` under the field name `file`. | Organiser (owner) | The image file | `200` with `{ url }`. `400` not an acceptable image. `503` storage not configured |
| GET | `/api/events/{id}/weather` | Race-day forecast for the event's coordinates, from Open-Meteo. | Any | None | `200` with the forecast, or `forecastAvailable: false` and a reason. `502` the weather service is down |

## 5. Categories

| Method | Route | Description | Role | Request body | Response |
|---|---|---|---|---|---|
| GET | `/api/events/{eventId}/categories` | The distances an event offers, with how many have entered each. | Any | None | `200` with the list. `404` unknown event |
| GET | `/api/categories/{id}` | One category on its own. | Any | None | `200`. `404` unknown category |
| POST | `/api/events/{eventId}/categories` | Adds a category. Names are unique within an event. | Organiser (owner) | `name, distanceKm, minAge, maxAge, entryFee` | `201`. `400` ages the wrong way round. `409` name already used on that event |
| PUT | `/api/categories/{id}` | Updates a category. The fee is locked once people have entered. | Organiser (owner) | Same as POST | `200`. `409` fee changed after entries, or name taken |
| DELETE | `/api/categories/{id}` | Removes a category nobody has entered. | Organiser (owner) | None | `204`. `409` people have already entered it |

## 6. Enrolments

| Method | Route | Description | Role | Request body | Response |
|---|---|---|---|---|---|
| POST | `/api/enrolments` | Enters the participant into one category. Checks the category belongs to the event, the event is still upcoming, the age limits are met and the event is not full. A race number is allocated automatically. | Participant | `eventId, categoryId` | `201` with the entry. `400` wrong event's category or an age limit not met. `409` already entered, full, or closed |
| GET | `/api/enrolments/{id}` | One entry. | Participant (owner) or Organiser (owner) | None | `200`. `403` somebody else's entry |
| GET | `/api/users/me/enrolments` | Everything the participant has entered, with results where they exist. Fills "My events". | Participant | None | `200` with the list |
| GET | `/api/events/{id}/enrolments` | The entry list for one event, filterable by `status`. | Organiser (owner) | None | `200` with the list |
| POST | `/api/enrolments/{id}/withdraw` | Withdraws an entry and frees the place. The row is kept as `Withdrawn` so history stays complete. | Participant (owner) | None | `200` with the entry. `409` already withdrawn, or a result exists |

## 7. Results

| Method | Route | Description | Role | Request body | Response |
|---|---|---|---|---|---|
| POST | `/api/results` | Captures a result for one entry, after the event has taken place. | Organiser (owner) | `enrolmentId, finishTime, position` | `201`. `400` position already used in that event. `409` result exists, entry withdrawn, or event not run yet |
| PUT | `/api/results/{id}` | Corrects a result after a timing dispute. | Organiser (owner) | Same as POST | `200`. `400` position already used |
| GET | `/api/results/{id}` | One result. | Any | None | `200`. `404` unknown result |
| GET | `/api/events/{id}/results` | The results for one event, fastest first. | Any | None | `200` with the list |
| GET | `/api/users/me/results` | The participant's own race history. Fills "My results". | Participant | None | `200` with the list |

---

## Why some of these work the way they do

**One entry per participant per event.** The database enforces this with a unique
constraint on `(ParticipantId, EventId)`, so re-entering after withdrawing reuses the
same row instead of failing on the constraint.

**Age limits use age on race day, not age today.** A junior category closes on the date
of the race, so the check works out the participant's age on `eventDate`.

**Positions are unique within an event.** There is no database constraint for this,
because positions only have to be unique inside one race, so the API checks it before
saving and answers 400 if the position is already taken.

**Images never touch the client.** Both upload endpoints take the file, store it in Azure
Blob Storage and return the URL. No front end ever holds an Azure credential.
