# SonarQube project quality

Impersonate supports an optional, informational, read-only SonarQube Server connection per project. It reads existing analysis through `/api/measures/component`; it does not install SonarQube, create projects, configure scanners or CI, or add a quality gate to planning, execution, review, or delivery.

Configure the SonarQube base URL, project key, optional display name, and a user token. Private projects require the token owner to have Browse permission. The token is write-only in the frontend and is encrypted with ASP.NET Core Data Protection in a credential table separate from AI-provider credentials. It is never returned in project or quality responses.

HTTPS is required by default. Development may explicitly enable HTTP for localhost. Other internal addresses are rejected unless the host is deliberately allowlisted in `CodeQuality:SonarQube:AllowedHosts`; redirects are not followed. Provider timeouts are bounded and safe failures do not make the project dashboard unavailable.

Metric availability varies by SonarQube Server version and configuration. Missing or malformed measures remain unavailable rather than becoming zero. The integration requests quality gate, coverage, new-code coverage, bugs, vulnerabilities, code smells, ratings, duplicated lines, lines of code, and cognitive complexity. The server's own `/api/metrics` and embedded Web API documentation remain authoritative.

Scanner and CI configuration remain outside this quick win. SonarQube results are informational only.
