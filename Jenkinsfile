// M2 · Demo 5 — build and test all three services in parallel; one JUnit report.
pipeline {
  agent { label 'linux-docker' }
  options {
    timeout(time: 30, unit: 'MINUTES')          // no runaway builds
    timestamps()                                 // Timestamper plugin
    buildDiscarder(logRotator(numToKeepStr: '30'))
    disableConcurrentBuilds()
  }
  stages {
    stage('Build & test') {
      parallel {
        stage('api (.NET)') {
          steps {
            dir('app/api') {
              sh 'dotnet test --logger "junit;LogFilePath=$WORKSPACE/results/api.xml"'
            }
          }
        }
        stage('worker (Python)') {
          steps {
            dir('app/worker') {
              sh '''
                python3 -m venv .venv
                .venv/bin/pip install -q -r requirements-dev.txt
                .venv/bin/python -m pytest --junit-xml="$WORKSPACE/results/worker.xml"
              '''
            }
          }
        }
        stage('web (Node.js)') {
          steps {
            dir('app/web') {
              sh '''
                npm ci
                node --test --test-reporter=spec --test-reporter-destination=stdout \
                            --test-reporter=junit --test-reporter-destination="$WORKSPACE/results/web.xml"
              '''
            }
          }
        }
      }
    }
  }
  post {
    always  { junit allowEmptyResults: true, testResults: 'results/*.xml' }
    fixed   { echo "Back to green: ${env.BUILD_URL}" }
    regression { echo "Was green, now red: ${env.BUILD_URL}" }
    cleanup { deleteDir() }
  }
}
