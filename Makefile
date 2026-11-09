# Common tasks. `make up` is the one-command local stack.
DOTNET ?= dotnet
NPM ?= npm

.PHONY: up down build test test-unit test-integration web-install web-test e2e dev-api dev-web format

up: ## Build and start SQL Server, API, web and the Aspire dashboard
	docker compose up --build

down: ## Stop the stack and keep data volumes
	docker compose down

build: ## Build the .NET solution and the Angular app
	$(DOTNET) build ClaimDesk.sln
	cd web && $(NPM) run build

test: test-unit test-integration web-test ## All fast tests (SQL Server tests skip without Docker)

test-unit:
	$(DOTNET) test tests/ClaimDesk.UnitTests

test-integration:
	$(DOTNET) test tests/ClaimDesk.IntegrationTests

web-install:
	cd web && $(NPM) ci

web-test: ## Angular unit tests in headless Chrome
	cd web && $(NPM) run test:ci

e2e: ## Playwright lifecycle E2E (starts its own API on SQLite and ng serve)
	cd web && npx playwright test

dev-api: ## API on http://localhost:5080 with SQLite and demo data
	ASPNETCORE_ENVIRONMENT=Development $(DOTNET) run --project src/ClaimDesk.Api --no-launch-profile --urls http://localhost:5080

dev-web: ## Angular dev server on http://localhost:4200, proxying /api to :5080
	cd web && $(NPM) start

format: ## Check .NET formatting
	$(DOTNET) format ClaimDesk.sln --verify-no-changes
