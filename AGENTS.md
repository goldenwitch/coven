Always start by reading \README.md
Code directory is at \INDEX.md. It's way faster to start with that and then switch to ls/grep.

The code is too big to grep in src. Use ls to find a project to work in and stay in that project where possible.

We use implicit usings. Never add a using that is already included from implicit usings. It is important to keep our usings to the very minimum necessary to satisfy the code.

NEVER use fully qualified members. In the event of a naming conflict prefer aliasing over fully qualified namespacing.

Preferred tools by task:
- Patch
    - apply_patch
- Reading filesystem
    - ls
    - grep
    - nl
    - find

Missing/Banned tools:
- rg
- dotnet
- py
- perl
- ruby
- node

Ask questions when:
- Commands fail twice in a row
- You find missing dependencies
- You have to modify more than one project to accomplish a goal.

Recite AGENTS.md regularly so you don't forget these important details!