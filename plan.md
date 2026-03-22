# AIDevBuddy - Project Plan

## Overview
AIDevBuddy is a .NET 10 Blazor WebApp Kanban dashboard implementing the BMAD (Breakthrough Method for Agile AI-Driven Development) framework.

## BMAD Framework Roles
- **Analyst**: Gathers requirements and produces initial briefs
- **Product Manager**: Produces PRDs and feature specifications
- **Architect**: Maps technical blueprints and architecture
- **Product Owner**: Refines specs and manages backlog
- **Scrum Master**: Translates specs to development stories
- **Developer**: Implements stories and writes code
- **QA Engineer**: Tests and validates implementations
- **Orchestrator**: Coordinates multi-agent interactions

## Technology Stack
- .NET 10 Blazor WebApp (server-side rendering with interactivity)
- Radzen Blazor component library (no JavaScript)
- SQLite via Entity Framework Core
- MSTest + Moq for unit tests

## Project Structure
- src/AIDevBuddy/ - Main Blazor WebApp project
- tests/AIDevBuddy.Tests/ - MSTest unit test project

## Key Features
1. Kanban Board with BMAD columns (Backlog, To Do, In Progress, Review, Done)
2. Agent management with status tracking
3. Agent messaging system with broadcast support
4. LLM provider configuration (Copilot, GLM, Claude, Local Ollama)
5. Dashboard with metrics and recent activity

## Implementation Phases
1. Project setup and data models
2. Database context and migrations
3. Service layer with interfaces
4. Blazor pages and components
5. Unit tests
