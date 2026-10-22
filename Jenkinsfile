// M3 · Demo 8 — build, scan and push images on main (adds to the M2 pipeline). Needs credential 'acr-push'.
pipeline {
  agent { label 'linux-docker' }
  options {
    timeout(time: 30, unit: 'MINUTES')          // no runaway builds
    timestamps()                                 // Timestamper plugin
    buildDiscarder(logRotator(numToKeepStr: '30'))
    disableConcurrentBuilds()
  }
  environment {
    // set ACR_LOGIN_SERVER once in Manage Jenkins → System → Global properties → Environment variables
    ACR = "${env.ACR_LOGIN_SERVER}"
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
                mkdir -p "$WORKSPACE/results"            
                node --test --test-reporter=spec --test-reporter-destination=stdout \
                            --test-reporter=junit --test-reporter-destination="$WORKSPACE/results/web.xml"
              '''
            }
          }
        }
      }
    }
    stage('Version') {
      steps {
        script {
          env.TAG = "1.0.${env.BUILD_NUMBER}-${sh(script: 'git rev-parse --short HEAD', returnStdout: true).trim()}"
          currentBuild.displayName = env.TAG
        }
      }
    }
    stage('Images') {                    // feature branches and PRs: build + test only
      stages {
        stage('Docker build') {
          steps {
            sh '''
              for svc in api worker web; do
                docker build --build-arg APP_VERSION="$TAG" -t "$ACR/orders-$svc:$TAG" "app/$svc"
              done
            '''
          }
        }
        stage('Push to ACR') {
          steps {
            script {
              docker.withRegistry("https://${env.ACR}", '8abc3f36-dbd1-47d0-84c5-dd1e07969236') {
                ['api', 'worker', 'web'].each { svc -> docker.image("${env.ACR}/orders-${svc}:${env.TAG}").push() }
              }
            }
          }
        }
      }
    }
    stage('Deploy dev') {
      steps {
        withKubeConfig([credentialsId: 'aks-kubeconfig']) {
          // everything except the Deployments, exactly as in Git
          sh 'kubectl apply -f deploy/k8s/dev/configmap.yaml -f deploy/k8s/dev/postgres.yaml -f deploy/k8s/dev/services.yaml -f deploy/k8s/dev/autoscaling.yaml -f deploy/k8s/dev/networkpolicies.yaml'
          // the Deployments with the image and version of THIS build (--local: change the YAML, don't touch the cluster)
          sh '''
            for svc in api worker web; do
              kubectl set image --local -o yaml -f "deploy/k8s/dev/$svc.yaml" "$svc=$ACR/orders-$svc:$TAG" \
                | kubectl set env --local -o yaml -f - "APP_VERSION=$TAG" \
                | kubectl apply -f -
            done
          '''
          sh '''
            for d in orders-api orders-worker orders-web; do
              kubectl -n dev rollout status "deploy/$d" --timeout=3m
            done
          '''
        }
      }
    }
    stage('Approve production') {
      steps {
        timeout(time: 1, unit: 'HOURS') {
          input message: "Deploy ${env.TAG} to production?", ok: 'Deploy'
        }
      }
    }
    stage('Deploy prod') {
      steps {
        withKubeConfig([credentialsId: 'aks-kubeconfig']) {
          sh 'kubectl apply -f deploy/k8s/prod/configmap.yaml -f deploy/k8s/prod/postgres.yaml -f deploy/k8s/prod/services.yaml -f deploy/k8s/prod/autoscaling.yaml -f deploy/k8s/prod/networkpolicies.yaml'
          sh '''
            for svc in api worker web; do
              kubectl set image --local -o yaml -f "deploy/k8s/prod/$svc.yaml" "$svc=$ACR/orders-$svc:$TAG" \
                | kubectl set env --local -o yaml -f - "APP_VERSION=$TAG" \
                | kubectl apply -f -
            done
          '''
          sh '''
            for d in orders-api orders-worker orders-web; do
              kubectl -n prod rollout status "deploy/$d" --timeout=3m
            done
          '''
          sh './deploy/smoke-test.sh prod "$TAG"'
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
