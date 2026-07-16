# FrostWyrm Archive
Welcome to the FrostWyrm Archive, a custom-built ECS to make tools and apps in the future. In theory, this should provide the needed resources to make just about any project I want to work on in the future, from work tools, to personal apps, to school projects, and maybe a game or two.
## General Structure
Take a look at the glossary for definitions of any terms, but in general this project is structured in a 3 part architecture.
### Part 1 - Core
The core is the... well... core of the ECS. It controls all the basics of entity, component, and system creation that will be used by modules and projects.
### Part 2 - Modules
Modules define the interface between the core and the project. These may be things like:
* A CLI Module
* A Blazor Module
* A Local App Module

Modules define control schemes and *where* a project lives.
### Part 3 - Projects
This is any code specific to an individual project.