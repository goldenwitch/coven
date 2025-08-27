# MagikBlock

A MagikBlock is our core logic unit. We compose MagikBlocks into trees.

## Functional Building
A MagikBuilder offers type-safe methods for constructing a Coven orchestration engine. This guarantees that every MagikBlock will only provide or be provided things it understands how to operate on.

After a MagikBuilder is finalized, the Coven engine is locked for changes (immutable). This ensures that there are no run-time surprises due to type mismatch.

## Builder Example
There are three separate ways to add a MagikBlock based on how you want to initialize it.

MagikBuilder
- .MagikBlock(builder => return m)
- .MagikBlock(new MagikBlock<T, T2>())
- .MagikBlock(T => { logic return t2 })
- .Done()

Each .MagikBlock returns a MagikBlockRegistration(T) object that itself transparently uses the MagikBuilder it is instantiated by.

## Overriding auto-tagging
By default the builder adds a tag to the outgoing messages for each MagikBlock that represents the downstream worker. That way each worker is automatically looking for work assigned to it. If you need to disable this for whatever reason .MagikBlock has an optional argument to specify a lambda that outputs a list of Tags with absolute control. This is in addition to any tags that the MagikBlock itself outputs.

The function that assigns tags MUST be static and only has access to limited information.

> As one more spicy caveat: Overriding tagging in this way can result in cycling. That's on you buddy; I warned you.

## Type limitations
Each MagikBlock supports generic TOutput and T, T2 ... T20 inputs.
- To handle arbitrary numbers of types of inputs, we dynamically generate T2 -> T20 based on usage.

