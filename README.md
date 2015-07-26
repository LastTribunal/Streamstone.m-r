# Streamstone.m-r
Greg Young's classic simple CQRS example using Streamstone as event store.

### Basic integration
`master` branch showcases the basic usage of Streamstone API in stateless CQRS/ES applications

### Inline projections
The more interesting is a demo of Streamstone's synchronous inline projections feature. Just switch to `inline_projections` to see and try it in action. It completes Greg's demo so that both events and projections are durably stored and are fully consistent. 

### Running demo
To run the demo you need to have Azure SDK installed and storage emulator started. The app was verified to run succesfully with Azure SDK 2.5 and VS2013. Use Azure storage explorer to inspect table storage in development storage account to see how Streamstone lays out streams/events and how projections created by app look like.
